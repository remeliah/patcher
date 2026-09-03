use serde::Serialize;
#[cfg(windows)]
use std::os::windows::ffi::OsStrExt;
#[cfg(windows)]
use std::os::windows::process::CommandExt;
use std::{
    collections::BTreeMap,
    env, fs, io,
    path::{Path, PathBuf},
    process::{Command, Stdio},
    time::{SystemTime, UNIX_EPOCH},
};
use tauri::Manager;
use thiserror::Error;

const CONFIG_DIR_NAME: &str = "osu! patcher";
const CONFIG_FILE_NAME: &str = "config.ini";
const DEFAULT_SERVER: &str = "refx.online";
const PATCHER_DLL: &str = "OsuPatcher.Runtime.dll";
const PATCHER_CLI_EXE: &str = "patcher-cli.exe";
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;
#[cfg(windows)]
const DETACHED_PROCESS: u32 = 0x00000008;
#[cfg(windows)]
const MOVE_FILE_REPLACE_EXISTING: u32 = 0x00000001;
#[cfg(windows)]
const MOVE_FILE_WRITE_THROUGH: u32 = 0x00000008;

#[cfg(windows)]
#[link(name = "kernel32")]
extern "system" {
    fn MoveFileExW(existing_file_name: *const u16, new_file_name: *const u16, flags: u32) -> i32;
}

#[derive(Debug, Error)]
enum AppError {
    #[error("LOCALAPPDATA is not available")]
    MissingLocalAppData,
    #[error("failed to read {path}: {source}")]
    Read { path: String, source: io::Error },
    #[error("failed to write {path}: {source}")]
    Write { path: String, source: io::Error },
    #[error("failed to create {path}: {source}")]
    CreateDir { path: String, source: io::Error },
    #[error("failed to open {path}: {source}")]
    Open { path: String, source: io::Error },
    #[error("osu!.exe was not found")]
    OsuNotFound,
    #[error("{path} does not contain osu!.exe")]
    InvalidOsuFolder { path: String },
    #[error("failed to launch {path}: {source}")]
    Launch { path: String, source: io::Error },
    #[error("patcher-cli.exe was not found; build patcher-cli in Release first")]
    CliNotFound,
    #[error("OsuPatcher.Runtime.dll was not found; build the C# runtime patcher in Release first")]
    PatcherNotFound,
    #[error("failed to copy {from} to {to}: {source}")]
    Copy {
        from: String,
        to: String,
        source: io::Error,
    },
    #[error("failed to query osu! process state: {0}")]
    ProcessQuery(io::Error),
}

impl From<AppError> for String {
    fn from(value: AppError) -> Self {
        value.to_string()
    }
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct PatcherConfig {
    patch_relax: bool,
    transition_time: bool,
    performance_calculator: bool,
    performance_counter_scale: f64,
    server: String,
    osu_path: Option<String>,
    path: String,
    artifact_path: String,
    artifact_exists: bool,
    created: bool,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct OsuState {
    path: Option<String>,
    running: bool,
}

impl Default for PatcherConfig {
    fn default() -> Self {
        Self {
            patch_relax: true,
            transition_time: true,
            performance_calculator: true,
            performance_counter_scale: 1.1,
            server: DEFAULT_SERVER.to_owned(),
            osu_path: None,
            path: String::new(),
            artifact_path: String::new(),
            artifact_exists: false,
            created: false,
        }
    }
}

#[tauri::command]
fn load_config(app: tauri::AppHandle) -> Result<PatcherConfig, String> {
    load_config_inner(&app).map_err(Into::into)
}

#[tauri::command]
fn open_path(app: tauri::AppHandle, kind: String) -> Result<(), String> {
    open_path_inner(&app, &kind).map_err(Into::into)
}

#[tauri::command]
fn detect_osu() -> Result<OsuState, String> {
    detect_osu_inner().map_err(Into::into)
}

#[tauri::command]
fn set_osu_installation(path: String) -> Result<OsuState, String> {
    set_osu_installation_inner(path).map_err(Into::into)
}

#[tauri::command]
fn open_osu_folder(path: String) -> Result<(), String> {
    open_osu_folder_inner(path).map_err(Into::into)
}

#[tauri::command]
fn launch_osu(
    app: tauri::AppHandle,
    path: Option<String>,
    server: Option<String>,
) -> Result<OsuState, String> {
    launch_osu_inner(&app, path, server).map_err(Into::into)
}

#[tauri::command]
fn inject_osu(
    app: tauri::AppHandle,
    path: Option<String>,
    server: Option<String>,
) -> Result<OsuState, String> {
    inject_osu_inner(&app, path, server).map_err(Into::into)
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            load_config,
            open_path,
            detect_osu,
            set_osu_installation,
            open_osu_folder,
            launch_osu,
            inject_osu,
        ])
        .run(tauri::generate_context!())
        .expect("failed to run osu! patcher app");
}

fn load_config_inner(app: &tauri::AppHandle) -> Result<PatcherConfig, AppError> {
    let config_path = config_path()?;
    let artifact_path = artifact_path(app);
    let mut created = false;

    if let Some(parent) = config_path.parent() {
        fs::create_dir_all(parent).map_err(|source| AppError::CreateDir {
            path: display_path(parent),
            source,
        })?;
    }

    let mut config = if config_path.exists() {
        parse_config(&config_path)?
    } else {
        created = true;
        PatcherConfig::default()
    };

    if created {
        write_config(&config_path, &config)?;
    }

    hydrate_paths(&mut config, &config_path, &artifact_path, created);
    Ok(config)
}

fn open_path_inner(app: &tauri::AppHandle, kind: &str) -> Result<(), AppError> {
    let target = match kind {
        "config" => config_dir()?,
        "artifact" => artifact_path(app),
        _ => config_dir()?,
    };

    let path = if kind == "artifact" {
        target.parent().map(Path::to_path_buf).unwrap_or(target)
    } else {
        target
    };

    Command::new("explorer")
        .arg(&path)
        .spawn()
        .map_err(|source| AppError::Open {
            path: display_path(&path),
            source,
        })?;

    Ok(())
}

fn detect_osu_inner() -> Result<OsuState, AppError> {
    Ok(OsuState {
        path: find_osu_executable().map(|path| display_path(&path)),
        running: is_osu_running()?,
    })
}

fn set_osu_installation_inner(path: String) -> Result<OsuState, AppError> {
    let selected = PathBuf::from(path.trim());
    let executable = if selected
        .file_name()
        .is_some_and(|name| name.eq_ignore_ascii_case("osu!.exe"))
    {
        selected.clone()
    } else {
        selected.join("osu!.exe")
    };

    if !executable.is_file() {
        return Err(AppError::InvalidOsuFolder {
            path: display_path(&selected),
        });
    }

    let config_path = config_path()?;
    if let Some(parent) = config_path.parent() {
        fs::create_dir_all(parent).map_err(|source| AppError::CreateDir {
            path: display_path(parent),
            source,
        })?;
    }

    update_config_entry(&config_path, "OsuPath", &display_path(&executable))?;

    Ok(OsuState {
        path: Some(display_path(&executable)),
        running: is_osu_running()?,
    })
}

fn open_osu_folder_inner(path: String) -> Result<(), AppError> {
    let executable = resolve_osu_path(Some(path))?;

    Command::new("explorer")
        .arg("/select,")
        .arg(&executable)
        .spawn()
        .map_err(|source| AppError::Open {
            path: display_path(&executable),
            source,
        })?;

    Ok(())
}

fn launch_osu_inner(
    app: &tauri::AppHandle,
    path: Option<String>,
    server: Option<String>,
) -> Result<OsuState, AppError> {
    let exe = resolve_osu_path(path)?;
    let server = normalize_server(server);
    save_server(&server)?;

    let mut command = Command::new(&exe);
    if let Some(parent) = exe.parent() {
        command.current_dir(parent);
    }

    command.arg("-devserver").arg(server);

    command.spawn().map_err(|source| AppError::Launch {
        path: display_path(&exe),
        source,
    })?;

    app.exit(0);

    Ok(OsuState {
        path: Some(display_path(&exe)),
        running: true,
    })
}

fn inject_osu_inner(
    app: &tauri::AppHandle,
    path: Option<String>,
    server: Option<String>,
) -> Result<OsuState, AppError> {
    let osu_path = resolve_osu_path(path)?;
    let patcher_path = resolve_patcher_artifact(app).ok_or(AppError::PatcherNotFound)?;
    prepare_patcher_dependencies(app, &patcher_path)?;
    let server = normalize_server(server);
    save_server(&server)?;

    let cli_path = find_patcher_cli(app).ok_or(AppError::CliNotFound)?;
    let cli_dir = cli_path.parent().unwrap_or_else(|| Path::new("."));

    let mut command = Command::new(&cli_path);
    command
        .current_dir(cli_dir)
        .arg("--osu")
        .arg(command_path(&osu_path))
        .arg("--patcher")
        .arg(command_path(&patcher_path))
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null());

    if !server.eq_ignore_ascii_case(DEFAULT_SERVER) {
        command.arg("--server").arg(server);
    }

    hide_console_window(&mut command);

    command.spawn().map_err(|source| AppError::Launch {
        path: display_path(&cli_path),
        source,
    })?;

    app.exit(0);

    Ok(OsuState {
        path: Some(display_path(&osu_path)),
        running: true,
    })
}

fn parse_config(path: &Path) -> Result<PatcherConfig, AppError> {
    let entries = read_config_entries(path)?;

    Ok(PatcherConfig {
        patch_relax: read_bool(&entries, "PatchRelax", true),
        transition_time: read_bool(&entries, "TransitionTime", true),
        performance_calculator: read_bool(&entries, "PerformanceCalculator", true),
        performance_counter_scale: read_f64(&entries, "PerformanceCounterScale", 1.1),
        server: read_string(&entries, "Server", DEFAULT_SERVER),
        osu_path: read_optional_string(&entries, "OsuPath"),
        ..PatcherConfig::default()
    })
}

fn write_config(path: &Path, config: &PatcherConfig) -> Result<(), AppError> {
    let mut entries = if path.exists() {
        read_config_entries(path)?
    } else {
        BTreeMap::new()
    };

    entries.insert("PatchRelax".to_owned(), config.patch_relax.to_string());
    entries.insert(
        "TransitionTime".to_owned(),
        config.transition_time.to_string(),
    );
    entries.insert(
        "PerformanceCalculator".to_owned(),
        config.performance_calculator.to_string(),
    );
    entries.insert(
        "PerformanceCounterScale".to_owned(),
        config.performance_counter_scale.to_string(),
    );
    entries.insert("Server".to_owned(), config.server.clone());

    if let Some(osu_path) = config
        .osu_path
        .as_ref()
        .filter(|value| !value.trim().is_empty())
    {
        entries.insert("OsuPath".to_owned(), osu_path.trim().to_owned());
    } else {
        entries.remove("OsuPath");
    }

    write_config_entries(path, entries)
}

fn read_config_entries(path: &Path) -> Result<BTreeMap<String, String>, AppError> {
    let content = fs::read_to_string(path).map_err(|source| AppError::Read {
        path: display_path(path),
        source,
    })?;

    Ok(content
        .lines()
        .filter_map(|line| line.split_once('='))
        .map(|(key, value)| (key.trim().to_owned(), value.trim().to_owned()))
        .filter(|(key, _)| !key.is_empty())
        .collect())
}

fn update_config_entry(path: &Path, key: &str, value: &str) -> Result<(), AppError> {
    let mut entries = if path.exists() {
        read_config_entries(path)?
    } else {
        BTreeMap::new()
    };
    entries.insert(key.to_owned(), value.to_owned());
    write_config_entries(path, entries)
}

fn write_config_entries(path: &Path, entries: BTreeMap<String, String>) -> Result<(), AppError> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|source| AppError::CreateDir {
            path: display_path(parent),
            source,
        })?;
    }

    let content = entries
        .into_iter()
        .map(|(key, value)| format!("{key}={value}\n"))
        .collect::<String>();

    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    let temporary_path = path.with_extension(format!("{}.{}.tmp", std::process::id(), nonce));

    fs::write(&temporary_path, content).map_err(|source| AppError::Write {
        path: display_path(&temporary_path),
        source,
    })?;

    if let Err(source) = replace_config_file(&temporary_path, path) {
        let _ = fs::remove_file(&temporary_path);
        return Err(AppError::Write {
            path: display_path(path),
            source,
        });
    }

    Ok(())
}

#[cfg(windows)]
fn replace_config_file(source: &Path, destination: &Path) -> io::Result<()> {
    let source = source
        .as_os_str()
        .encode_wide()
        .chain(std::iter::once(0))
        .collect::<Vec<_>>();
    let destination = destination
        .as_os_str()
        .encode_wide()
        .chain(std::iter::once(0))
        .collect::<Vec<_>>();
    let result = unsafe {
        MoveFileExW(
            source.as_ptr(),
            destination.as_ptr(),
            MOVE_FILE_REPLACE_EXISTING | MOVE_FILE_WRITE_THROUGH,
        )
    };

    if result == 0 {
        Err(io::Error::last_os_error())
    } else {
        Ok(())
    }
}

#[cfg(not(windows))]
fn replace_config_file(source: &Path, destination: &Path) -> io::Result<()> {
    fs::rename(source, destination)
}

fn read_bool(entries: &BTreeMap<String, String>, key: &str, default: bool) -> bool {
    entries
        .get(key)
        .and_then(|value| value.parse::<bool>().ok())
        .unwrap_or(default)
}

fn read_f64(entries: &BTreeMap<String, String>, key: &str, default: f64) -> f64 {
    entries
        .get(key)
        .and_then(|value| value.parse::<f64>().ok())
        .filter(|value| value.is_finite())
        .unwrap_or(default)
}

fn read_string(entries: &BTreeMap<String, String>, key: &str, default: &str) -> String {
    entries
        .get(key)
        .map(|value| value.trim())
        .filter(|value| !value.is_empty())
        .unwrap_or(default)
        .to_owned()
}

fn read_optional_string(entries: &BTreeMap<String, String>, key: &str) -> Option<String> {
    entries
        .get(key)
        .map(|value| value.trim())
        .filter(|value| !value.is_empty())
        .map(str::to_owned)
}

fn save_server(server: &str) -> Result<(), AppError> {
    update_config_entry(&config_path()?, "Server", server)
}

fn hydrate_paths(
    config: &mut PatcherConfig,
    config_path: &Path,
    artifact_path: &Path,
    created: bool,
) {
    config.path = display_path(config_path);
    config.artifact_path = display_path(artifact_path);
    config.artifact_exists = artifact_path.exists();
    config.created = created;
}

fn config_path() -> Result<PathBuf, AppError> {
    Ok(config_dir()?.join(CONFIG_FILE_NAME))
}

fn config_dir() -> Result<PathBuf, AppError> {
    env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .map(|path| path.join(CONFIG_DIR_NAME))
        .ok_or(AppError::MissingLocalAppData)
}

fn find_osu_executable() -> Option<PathBuf> {
    if let Some(saved) = saved_osu_executable() {
        return Some(saved);
    }

    let local_app_data = env::var_os("LOCALAPPDATA").map(PathBuf::from)?;
    let preferred = local_app_data.join("osu!").join("osu!.exe");
    if preferred.exists() {
        return Some(preferred);
    }

    fs::read_dir(local_app_data)
        .ok()?
        .filter_map(Result::ok)
        .map(|entry| entry.path().join("osu!.exe"))
        .find(|path| path.exists())
}

fn saved_osu_executable() -> Option<PathBuf> {
    let config_path = config_path().ok()?;
    let config = parse_config(&config_path).ok()?;
    let executable = PathBuf::from(config.osu_path?);
    executable.is_file().then_some(executable)
}

fn resolve_osu_path(path: Option<String>) -> Result<PathBuf, AppError> {
    path.filter(|value| !value.trim().is_empty())
        .map(PathBuf::from)
        .or_else(find_osu_executable)
        .filter(|path| path.exists())
        .ok_or(AppError::OsuNotFound)
}

fn normalize_server(server: Option<String>) -> String {
    server
        .map(|value| value.trim().to_owned())
        .filter(|value| !value.is_empty())
        .unwrap_or_else(|| DEFAULT_SERVER.to_owned())
}

fn find_patcher_cli(app: &tauri::AppHandle) -> Option<PathBuf> {
    resource_path(app, PATCHER_CLI_EXE)
        .into_iter()
        .chain(repo_root().into_iter().flat_map(|root| {
            [
                root.join("OsuPatcher.Cli")
                    .join("bin")
                    .join("Release")
                    .join(PATCHER_CLI_EXE),
                root.join("OsuPatcher.Cli")
                    .join("bin")
                    .join("Debug")
                    .join(PATCHER_CLI_EXE),
            ]
        }))
        .find(|path| path.exists())
}

fn resolve_patcher_artifact(app: &tauri::AppHandle) -> Option<PathBuf> {
    resource_path(app, PATCHER_DLL)
        .into_iter()
        .chain(repo_root().into_iter().flat_map(|root| {
            [
                patcher_artifact_path(&root, "Release"),
                patcher_artifact_path(&root, "Debug"),
            ]
        }))
        .find(|path| path.exists())
}

fn prepare_patcher_dependencies(
    app: &tauri::AppHandle,
    patcher_path: &Path,
) -> Result<(), AppError> {
    let Some(patcher_dir) = patcher_path.parent() else {
        return Ok(());
    };

    let harmony_target = patcher_dir.join("0Harmony.dll");
    if !harmony_target.exists() {
        if let Some(harmony_source) = harmony_source(app) {
            fs::copy(&harmony_source, &harmony_target).map_err(|source| AppError::Copy {
                from: display_path(&harmony_source),
                to: display_path(&harmony_target),
                source,
            })?;
        }
    }

    let refx_target = patcher_dir.join("refx_ffi.dll");
    if !refx_target.exists() {
        if let Some(refx_source) = refx_ffi_source(app) {
            fs::copy(&refx_source, &refx_target).map_err(|source| AppError::Copy {
                from: display_path(&refx_source),
                to: display_path(&refx_target),
                source,
            })?;
        }
    }

    Ok(())
}

fn refx_ffi_source(app: &tauri::AppHandle) -> Option<PathBuf> {
    resource_path(app, "refx_ffi.dll")
        .into_iter()
        .chain(repo_root().map(|root| {
            root.join("OsuPatcher.PP")
                .join("target")
                .join("i686-pc-windows-msvc")
                .join("release")
                .join("refx_ffi.dll")
        }))
        .find(|path| path.exists())
}

fn harmony_source(app: &tauri::AppHandle) -> Option<PathBuf> {
    resource_path(app, "0Harmony.dll")
        .into_iter()
        .chain(repo_root().map(|root| {
            root.join("packages")
                .join("Lib.Harmony.2.3.3")
                .join("lib")
                .join("net472")
                .join("0Harmony.dll")
        }))
        .chain(
            repo_root()
                .map(|root| patcher_artifact_path(&root, "Release").with_file_name("0Harmony.dll")),
        )
        .find(|path| path.exists())
}

fn is_osu_running() -> Result<bool, AppError> {
    let mut command = Command::new("tasklist");
    command.args(["/FI", "IMAGENAME eq osu!.exe", "/FO", "CSV", "/NH"]);
    suppress_console_window(&mut command);

    let output = command.output().map_err(AppError::ProcessQuery)?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    Ok(stdout.lines().any(|line| line.contains("\"osu!.exe\"")))
}

fn artifact_path(app: &tauri::AppHandle) -> PathBuf {
    resource_path(app, PATCHER_DLL)
        .or_else(|| {
            repo_root()
                .map(|root| patcher_artifact_path(&root, "Release"))
                .filter(|path| path.exists())
        })
        .unwrap_or_else(|| {
            repo_root()
                .map(|root| patcher_artifact_path(&root, "Release"))
                .unwrap_or_else(|| {
                    PathBuf::from("OsuPatcher.Runtime")
                        .join("bin")
                        .join("Release")
                        .join(PATCHER_DLL)
                })
        })
}

fn patcher_artifact_path(root: &Path, configuration: &str) -> PathBuf {
    root.join("OsuPatcher.Runtime")
        .join("bin")
        .join(configuration)
        .join(PATCHER_DLL)
}

fn repo_root() -> Option<PathBuf> {
    let manifest_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    manifest_dir
        .parent()
        .and_then(Path::parent)
        .map(Path::to_path_buf)
}

fn resource_path(app: &tauri::AppHandle, name: &str) -> Option<PathBuf> {
    app.path()
        .resolve(name, tauri::path::BaseDirectory::Resource)
        .ok()
}

fn display_path(path: &Path) -> String {
    path.to_string_lossy().into_owned()
}

fn command_path(path: &Path) -> String {
    let path = display_path(path);

    if let Some(stripped) = path.strip_prefix(r"\\?\UNC\") {
        format!(r"\\{}", stripped)
    } else if let Some(stripped) = path.strip_prefix(r"\\?\") {
        stripped.to_owned()
    } else {
        path
    }
}

#[cfg(windows)]
fn hide_console_window(command: &mut Command) {
    command.creation_flags(CREATE_NO_WINDOW | DETACHED_PROCESS);
}

#[cfg(not(windows))]
fn hide_console_window(_: &mut Command) {}

#[cfg(windows)]
fn suppress_console_window(command: &mut Command) {
    command.creation_flags(CREATE_NO_WINDOW);
}

#[cfg(not(windows))]
fn suppress_console_window(_: &mut Command) {}

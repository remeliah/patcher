use serde::{Deserialize, Serialize};
#[cfg(windows)]
use std::os::windows::process::CommandExt;
use std::{
    collections::BTreeMap,
    env, fs, io,
    path::{Path, PathBuf},
    process::{Command, Stdio},
};
use tauri::Manager;
use thiserror::Error;

const CONFIG_DIR_NAME: &str = "osuPatcher";
const CONFIG_FILE_NAME: &str = "config.ini";
const OSU_PATH_FILE_NAME: &str = "osu-path.txt";
const DEFAULT_SERVER: &str = "refx.online";
const PATCHER_DLL: &str = "OsuPatcher.Runtime.dll";
const PATCHER_CLI_EXE: &str = "patcher-cli.exe";
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;
#[cfg(windows)]
const DETACHED_PROCESS: u32 = 0x00000008;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct PatcherConfig {
    patch_relax: bool,
    transition_time: bool,
    performance_calculator: bool,
    server: String,
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
            server: DEFAULT_SERVER.to_owned(),
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
fn save_config(app: tauri::AppHandle, config: PatcherConfig) -> Result<PatcherConfig, String> {
    save_config_inner(&app, &config).map_err(Into::into)
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
            save_config,
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
        let default = PatcherConfig::default();
        write_config(&config_path, &default)?;
        default
    };

    hydrate_paths(&mut config, &config_path, &artifact_path, created);
    Ok(config)
}

fn save_config_inner(
    app: &tauri::AppHandle,
    config: &PatcherConfig,
) -> Result<PatcherConfig, AppError> {
    let config_path = config_path()?;
    let artifact_path = artifact_path(app);

    if let Some(parent) = config_path.parent() {
        fs::create_dir_all(parent).map_err(|source| AppError::CreateDir {
            path: display_path(parent),
            source,
        })?;
    }

    let mut saved = config.clone();
    saved.server = normalize_server(Some(saved.server));
    write_config(&config_path, &saved)?;
    hydrate_paths(&mut saved, &config_path, &artifact_path, false);
    Ok(saved)
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

    let path_file = osu_path_file()?;
    if let Some(parent) = path_file.parent() {
        fs::create_dir_all(parent).map_err(|source| AppError::CreateDir {
            path: display_path(parent),
            source,
        })?;
    }

    fs::write(&path_file, display_path(&executable)).map_err(|source| AppError::Write {
        path: display_path(&path_file),
        source,
    })?;

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
    save_server(app, &server)?;

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
    save_server(app, &server)?;

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
    let content = fs::read_to_string(path).map_err(|source| AppError::Read {
        path: display_path(path),
        source,
    })?;

    let entries = content
        .lines()
        .filter_map(|line| line.split_once('='))
        .map(|(key, value)| (key.trim().to_owned(), value.trim().to_owned()))
        .collect::<BTreeMap<_, _>>();

    Ok(PatcherConfig {
        patch_relax: read_bool(&entries, "PatchRelax", true),
        transition_time: read_bool(&entries, "TransitionTime", true),
        performance_calculator: read_bool(&entries, "PerformanceCalculator", true),
        server: read_string(&entries, "Server", DEFAULT_SERVER),
        ..PatcherConfig::default()
    })
}

fn write_config(path: &Path, config: &PatcherConfig) -> Result<(), AppError> {
    let content = format!(
        "PatchRelax={}\nTransitionTime={}\nPerformanceCalculator={}\nServer={}\n",
        config.patch_relax, config.transition_time, config.performance_calculator, config.server
    );

    fs::write(path, content).map_err(|source| AppError::Write {
        path: display_path(path),
        source,
    })
}

fn read_bool(entries: &BTreeMap<String, String>, key: &str, default: bool) -> bool {
    entries
        .get(key)
        .and_then(|value| value.parse::<bool>().ok())
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

fn save_server(app: &tauri::AppHandle, server: &str) -> Result<(), AppError> {
    let mut config = load_config_inner(app)?;
    config.server = server.to_owned();
    save_config_inner(app, &config)?;
    Ok(())
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

fn osu_path_file() -> Result<PathBuf, AppError> {
    Ok(config_dir()?.join(OSU_PATH_FILE_NAME))
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
    let path_file = osu_path_file().ok()?;
    let saved = fs::read_to_string(path_file).ok()?;
    let executable = PathBuf::from(saved.trim());
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

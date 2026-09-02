import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import { getCurrentWindow } from "@tauri-apps/api/window";
import "@fontsource-variable/bricolage-grotesque/wdth.css";
import "@fontsource-variable/ibm-plex-sans/wght.css";
import "@fontsource-variable/jetbrains-mono/wght.css";
import "./styles.css";

const DEFAULT_SERVER = "refx.online";

const state = {
  osu: null,
  serverInput: DEFAULT_SERVER,
  status: "Checking your installation…",
  statusTone: "loading",
  launchState: "default",
  busy: false,
  choosingFolder: false,
};

const app = document.querySelector("#app");
const appWindow = window.__TAURI_INTERNALS__ ? getCurrentWindow() : null;

function render() {
  const controlsLocked = state.busy || state.choosingFolder;
  const installationFound = Boolean(state.osu?.path);
  const installationLabel = state.choosingFolder
    ? "Opening folder picker…"
    : installationFound
      ? `Found osu! at ${state.osu.path}`
      : "osu! not found — locate";
  const installationHelp = state.statusTone === "error"
    ? state.status
    : installationFound
      ? "Click to open the installation folder"
      : "Click to choose the folder containing osu!.exe";

  app.innerHTML = `
    <svg class="liquid-defs" aria-hidden="true" focusable="false">
      <defs>
        <filter id="button-goo" x="-30%" y="-80%" width="160%" height="260%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="6" result="blur" />
          <feColorMatrix
            in="blur"
            mode="matrix"
            values="1 0 0 0 0  0 1 0 0 0  0 0 1 0 0  0 0 0 24 -10"
            result="goo"
          />
          <feComposite in="SourceGraphic" in2="goo" operator="atop" />
        </filter>
      </defs>
    </svg>

    <div class="window-shell">
      <header class="window-bar" data-tauri-drag-region>
        <p class="window-wordmark" data-tauri-drag-region>
          osu!patcher
        </p>
        <button class="window-close" id="close-window" type="button" aria-label="Close launcher" title="Close">
          <span aria-hidden="true">×</span>
        </button>
      </header>

      <main class="launcher-main">
        <section class="identity" aria-label="runtime status">
          <div class="liquid-stage" aria-hidden="true">
            <div class="liquid-core" data-tone="${state.statusTone}">
              <span class="liquid-orbit"></span>
              <span class="liquid-mass liquid-mass-one"></span>
              <span class="liquid-mass liquid-mass-two"></span>
              <span class="liquid-pip"></span>
              <span class="liquid-drop liquid-drop-one"></span>
              <span class="liquid-drop liquid-drop-two"></span>
            </div>
          </div>
        </section>

        <section class="runtime-state" data-tone="${state.statusTone}" aria-label="osu! installation">
          <button
            id="osu-installation"
            class="installation-link"
            type="button"
            data-state="${installationFound ? "found" : "missing"}"
            aria-describedby="installation-help"
            title="${installationFound ? "Open the osu! installation folder" : "Choose the osu! installation folder"}"
            ${controlsLocked ? "disabled" : ""}
          >
            <span class="installation-symbol" aria-hidden="true"></span>
            <span class="installation-text">${escapeHtml(installationLabel)}</span>
          </button>
          <p
            class="installation-help"
            id="installation-help"
            role="status"
            aria-live="polite"
            title="${escapeAttribute(installationHelp)}"
          >${escapeHtml(installationHelp)}</p>
        </section>

        <label class="server-field" for="server">
          <span class="server-label">server endpoint</span>
          <span class="server-input-frame" data-filled="${Boolean(state.serverInput.trim())}">
            <span class="server-prompt" aria-hidden="true">://</span>
            <input
              id="server"
              type="text"
              value="${escapeAttribute(state.serverInput)}"
              placeholder="refx.online"
              autocomplete="off"
              spellcheck="false"
              ${controlsLocked ? "disabled" : ""}
            />
          </span>
        </label>

        <button
          id="patch-osu"
          class="primary-action"
          type="button"
          data-state="${state.launchState}"
          aria-busy="${state.busy}"
          ${controlsLocked || !installationFound ? "disabled" : ""}
        >
          <span class="primary-liquid" aria-hidden="true">
            <span class="primary-wave"></span>
            <span class="primary-blob primary-blob-one"></span>
            <span class="primary-blob primary-blob-two"></span>
          </span>
          <span class="primary-label">${actionLabel()}</span>
          <span class="primary-arrow" aria-hidden="true">${actionGlyph()}</span>
        </button>
      </main>
    </div>
  `;

  document.querySelector("#close-window").addEventListener("click", () => appWindow?.close());
  document.querySelector("#patch-osu").addEventListener("click", patchAndLaunchOsu);
  document.querySelector("#osu-installation").addEventListener("click", handleInstallationClick);
  document.querySelector("#server").addEventListener("input", (event) => {
    state.serverInput = event.target.value;
    state.launchState = "default";
    event.target.parentElement.dataset.filled = String(Boolean(event.target.value.trim()));
  });
}

function actionLabel() {
  if (state.launchState === "loading") return "Injecting";
  if (state.launchState === "success") return "Launched";
  if (state.launchState === "error") return "Try again";
  return "Launch osu!";
}

function actionGlyph() {
  if (state.launchState === "loading") return "";
  if (state.launchState === "success") return "✓";
  if (state.launchState === "error") return "↻";
  return "↗";
}

async function loadState() {
  await run("Checking your installation…", async () => {
    const [config, osu] = await Promise.all([
      invoke("load_config"),
      invoke("detect_osu"),
    ]);
    state.serverInput = config.server?.trim() || DEFAULT_SERVER;
    state.osu = osu;
    state.status = state.osu.path
      ? "Installation found — ready to patch"
      : "Installation not found — choose its folder";
    state.statusTone = state.osu.path ? "ready" : "attention";
  });
}

async function handleInstallationClick() {
  if (state.osu?.path) {
    await run("Opening your osu! folder…", async () => {
      await invoke("open_osu_folder", { path: state.osu.path });
      state.status = "Installation found — ready to patch";
      state.statusTone = "ready";
    }, "installation");
    return;
  }

  state.choosingFolder = true;
  render();

  try {
    const selected = await open({
      directory: true,
      multiple: false,
      title: "Choose your osu! installation folder",
    });

    if (selected) {
      state.osu = await invoke("set_osu_installation", { path: selected });
      state.status = "Installation saved — ready to patch";
      state.statusTone = "ready";
    }
  } catch (error) {
    state.status = formatError(error, "installation");
    state.statusTone = "error";
  } finally {
    state.choosingFolder = false;
    render();
  }
}

async function patchAndLaunchOsu() {
  await run("Preparing osu!…", async () => {
    state.osu = await invoke("inject_osu", {
      path: state.osu?.path || null,
      server: state.serverInput.trim() || DEFAULT_SERVER,
    });
    state.status = "osu! launched with patches";
    state.statusTone = "ready";
  }, "launch");
}

async function run(message, operation, context = "load") {
  state.busy = true;
  state.status = message;
  state.statusTone = "loading";
  if (context === "launch") state.launchState = "loading";
  render();

  try {
    await operation();
    if (context === "launch") state.launchState = "success";
  } catch (error) {
    state.status = formatError(error, context);
    state.statusTone = "error";
    if (context === "launch") state.launchState = "error";
  } finally {
    state.busy = false;
    render();
  }
}

function formatError(error, context) {
  const detail = String(error);

  if (detail.includes("osu!.exe was not found")) {
    return "osu!.exe wasn’t found. Choose the folder where osu! is installed.";
  }

  if (detail.includes("does not contain osu!.exe")) {
    return "That folder doesn’t contain osu!.exe. Choose the osu! installation folder.";
  }

  if (detail.includes("patcher-cli.exe was not found")) {
    return "The launcher component is missing. Reinstall osu! patcher.";
  }

  if (detail.includes("OsuPatcher.Runtime.dll was not found")) {
    return "The runtime patch is missing. Reinstall osu! patcher.";
  }

  if (context === "installation") {
    return `The installation folder couldn’t be opened. ${detail}`;
  }

  if (context === "launch") {
    return `osu! couldn’t be launched. ${detail}`;
  }

  return `The setup couldn’t be loaded. ${detail}`;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function escapeAttribute(value) {
  return escapeHtml(value).replaceAll("`", "&#096;");
}

render();
loadState();

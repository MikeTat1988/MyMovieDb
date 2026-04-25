const state = {
    codexStart: null,
    codexElapsed: 0,
    lastReport: null
};

const $ = (selector, root = document) => root.querySelector(selector);

const statusEl = $("#status");
const modelEl = $("#model");
const codexModelEl = $("#codexModel");
const resultsEl = $("#results");
const template = $("#movieTemplate");

function formatMs(value) {
    if (value === null || value === undefined) return "-";
    if (value < 1000) return `${value} ms`;
    return `${(value / 1000).toFixed(1)} s`;
}

function setBusy(isBusy) {
    $("#loadRaw").disabled = isBusy;
    $("#runOllama").disabled = isBusy;
    $("#runCodex").disabled = isBusy;
    $("#runCompare").disabled = isBusy;
    $("#saveReport").disabled = isBusy;
}

async function fetchJson(url, options) {
    const response = await fetch(url, options);
    if (!response.ok) {
        throw new Error(await response.text());
    }
    return response.json();
}

async function initialize() {
    try {
        const health = await fetchJson("/api/health");
        statusEl.textContent = `Connected to ${health.lab}; DB: ${health.database}`;
    } catch (error) {
        statusEl.textContent = `Lab health failed: ${error.message}`;
    }

    await loadModels();
    await loadCodexModels();
    bindEvents();
}

async function loadModels() {
    modelEl.innerHTML = `<option value="">No model selected</option>`;
    try {
        const result = await fetchJson("/api/models");
        if (!result.ok) {
            modelEl.innerHTML = `<option value="">Ollama unavailable</option>`;
            statusEl.textContent = `Ollama check failed after ${formatMs(result.elapsedMs)}: ${result.error}`;
            return;
        }

        if (result.models.length === 0) {
            modelEl.innerHTML = `<option value="">No Ollama models installed</option>`;
            return;
        }

        modelEl.innerHTML = result.models
            .map(name => `<option value="${escapeHtml(name)}">${escapeHtml(name)}</option>`)
            .join("");
    } catch (error) {
        modelEl.innerHTML = `<option value="">Ollama unavailable</option>`;
        statusEl.textContent = `Ollama model load failed: ${error.message}`;
    }
}

async function loadCodexModels() {
    codexModelEl.innerHTML = `<option value="">No Codex model selected</option>`;
    try {
        const result = await fetchJson("/api/codex/models");
        if (!result.ok) {
            codexModelEl.innerHTML = `<option value="">Codex unavailable</option>`;
            statusEl.textContent = `Codex check failed after ${formatMs(result.elapsedMs)}: ${result.error}`;
            return;
        }

        if (result.models.length === 0) {
            codexModelEl.innerHTML = `<option value="">No Codex models found</option>`;
            return;
        }

        codexModelEl.innerHTML = result.models
            .map(name => `<option value="${escapeHtml(name)}">${escapeHtml(labelCodexModel(name))}</option>`)
            .join("");
        if (result.models.includes("gpt-5.4-mini")) {
            codexModelEl.value = "gpt-5.4-mini";
        }
    } catch (error) {
        codexModelEl.innerHTML = `<option value="">Codex unavailable</option>`;
        statusEl.textContent = `Codex model load failed: ${error.message}`;
    }
}

function bindEvents() {
    $("#loadRaw").addEventListener("click", loadRaw);
    $("#runOllama").addEventListener("click", runOllama);
    $("#runCodex").addEventListener("click", runCodex);
    $("#runCompare").addEventListener("click", runCompare);
    $("#saveReport").addEventListener("click", saveReport);
    $("#codexStart").addEventListener("click", () => {
        state.codexStart = performance.now();
        state.codexElapsed = 0;
        $("#codexElapsed").textContent = "Running...";
    });
    $("#codexStop").addEventListener("click", () => {
        if (state.codexStart !== null) {
            state.codexElapsed = Math.round(performance.now() - state.codexStart);
            state.codexStart = null;
            $("#codexElapsed").textContent = formatMs(state.codexElapsed);
            $("#timing").children[2].querySelector("strong").textContent = formatMs(state.codexElapsed);
        }
    });
}

async function runCodex() {
    setBusy(true);
    try {
        const payload = {
            count: Number($("#count").value || 5),
            model: codexModelEl.value || "gpt-5.4-mini",
            temperature: 0
        };
        const data = await fetchJson("/api/codex/analyze", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        state.lastReport = {
            createdAt: new Date().toISOString(),
            mode: "codex",
            request: payload,
            results: data.results,
            timing: data.timing,
            notice: data.notice
        };
        renderResults(data.results);
        setTiming(data.timing.rawAppMs, data.timing.ollamaMs, data.timing.codexMs, data.timing.totalMs);
        statusEl.textContent = data.notice || `Codex analyzed ${data.results.length} candidates.`;
    } catch (error) {
        statusEl.textContent = `Codex analysis failed: ${error.message}`;
    } finally {
        setBusy(false);
    }
}

async function runCompare() {
    setBusy(true);
    try {
        const payload = {
            count: Number($("#count").value || 5),
            ollamaModel: modelEl.value,
            temperature: Number($("#temperature").value || 0.3)
        };
        const data = await fetchJson("/api/compare", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        state.lastReport = {
            createdAt: new Date().toISOString(),
            mode: "compare",
            request: payload,
            results: data.results,
            timing: data.timing,
            notice: data.notice
        };
        renderResults(data.results);
        setTiming(data.timing.rawAppMs, data.timing.ollamaMs, data.timing.codexMs, data.timing.totalMs);
        statusEl.textContent = data.notice || `Compared Ollama, gpt-5.4-mini, and gpt-5.5 for ${data.results.length} candidates. ${formatRunSummary(data.results)}`;
    } catch (error) {
        statusEl.textContent = `Comparison failed: ${error.message}`;
    } finally {
        setBusy(false);
    }
}

async function loadRaw() {
    setBusy(true);
    try {
        const count = Number($("#count").value || 5);
        const data = await fetchJson(`/api/candidates?count=${encodeURIComponent(count)}`);
        const results = data.candidates.map(candidate => ({ candidate, ollama: null }));
        state.lastReport = {
            createdAt: new Date().toISOString(),
            mode: "raw",
            results,
            timing: {
                rawAppMs: data.rawElapsedMs,
                ollamaMs: null,
                codexMs: null,
                totalMs: data.rawElapsedMs
            }
        };
        renderResults(results);
        setTiming(data.rawElapsedMs, null, null, data.rawElapsedMs);
        statusEl.textContent = `Loaded ${data.candidates.length} raw candidates.`;
    } catch (error) {
        statusEl.textContent = `Raw load failed: ${error.message}`;
    } finally {
        setBusy(false);
    }
}

async function runOllama() {
    setBusy(true);
    try {
        const payload = {
            count: Number($("#count").value || 5),
            model: modelEl.value,
            temperature: Number($("#temperature").value || 0.3)
        };
        const data = await fetchJson("/api/analyze", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        state.lastReport = {
            createdAt: new Date().toISOString(),
            mode: "ollama",
            request: payload,
            results: data.results,
            timing: data.timing,
            notice: data.notice
        };
        renderResults(data.results);
        setTiming(data.timing.rawAppMs, data.timing.ollamaMs, data.timing.codexMs, data.timing.totalMs);
        statusEl.textContent = data.notice || `Analyzed ${data.results.length} candidates.`;
    } catch (error) {
        statusEl.textContent = `Ollama analysis failed: ${error.message}`;
    } finally {
        setBusy(false);
    }
}

async function saveReport() {
    if (!state.lastReport) {
        statusEl.textContent = "Run Raw MovieDb or Ollama Analysis before saving a report.";
        return;
    }

    setBusy(true);
    try {
        const report = {
            ...state.lastReport,
            codexElapsedMs: state.codexElapsed || null,
            codexNotes: collectCodexNotes()
        };
        const saved = await fetchJson("/api/reports/last-run", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                payload: report,
                markdown: buildMarkdownReport(report)
            })
        });
        statusEl.textContent = `Saved report: ${saved.markdownPath}`;
    } catch (error) {
        statusEl.textContent = `Save failed: ${error.message}`;
    } finally {
        setBusy(false);
    }
}

function collectCodexNotes() {
    return Array.from(document.querySelectorAll(".movie-card")).map(card => ({
        title: $(".js-title", card).textContent,
        text: $(".js-codex-notes", card).value
    })).filter(item => item.text.trim().length > 0);
}

function buildMarkdownReport(report) {
    const lines = [
        "# MovieDb LLM Lab Report",
        "",
        `Created: ${report.createdAt}`,
        `Mode: ${report.mode}`,
        "",
        "## Timing",
        "",
        `- Raw app: ${formatMs(report.timing?.rawAppMs)}`,
        `- Ollama: ${formatMs(report.timing?.ollamaMs)}`,
        `- Codex: ${report.codexElapsedMs ? formatMs(report.codexElapsedMs) : "manual / not recorded"}`,
        `- Median elapsed: ${formatRunSummary(report.results) || "not available"}`,
        `- Codex tokens: ${sumCodexTokens(report.results)}`,
        `- Median Codex tokens/call: ${formatMedianCodexTokens(report.results)}`,
        `- Total: ${formatMs(report.timing?.totalMs)}`,
        ""
    ];

    for (const item of report.results || []) {
        const raw = item.candidate.raw;
        lines.push(`## ${raw.title}${raw.year ? ` (${raw.year})` : ""}`);
        lines.push("");
        lines.push("### MovieDb Raw");
        lines.push("");
        lines.push(`- Score: ${raw.predictedScore}`);
        lines.push(`- Label: ${raw.predictedLabel}`);
        lines.push(`- Reason: ${raw.predictedReason || "none"}`);
        lines.push(`- Tags: ${raw.reasonTags || "none"}`);
        lines.push(`- Positive factors: ${raw.positiveFactors.join("; ") || "none"}`);
        lines.push(`- Negative factors: ${raw.negativeFactors.join("; ") || "none"}`);
        lines.push(`- Liked anchors: ${raw.similarLiked.join("; ") || "none"}`);
        lines.push(`- Disliked anchors: ${raw.similarDisliked.join("; ") || "none"}`);
        lines.push("");
        lines.push("### Ollama");
        lines.push("");
        if (item.ollama) {
            lines.push(`Elapsed: ${formatMs(item.ollama.elapsedMs)}`);
            lines.push("");
            lines.push(item.ollama.ok ? item.ollama.text : `Ollama failed: ${item.ollama.error}`);
        } else {
            lines.push("Not run.");
        }
        lines.push("");
        lines.push("### Codex");
        lines.push("");
        if (item.codex) {
            lines.push(`Model: ${item.codex.model}`);
            lines.push(`Elapsed: ${formatMs(item.codex.elapsedMs)}`);
            lines.push(`Tokens: ${formatTokens(item.codex)}`);
            lines.push("");
            lines.push(item.codex.ok ? item.codex.text : `Codex failed: ${item.codex.error}`);
        } else if (item.codexRuns?.length) {
            for (const run of item.codexRuns) {
                lines.push(`#### ${run.model}`);
                lines.push("");
                lines.push(`Elapsed: ${formatMs(run.elapsedMs)}`);
                lines.push(`Tokens: ${formatTokens(run)}`);
                lines.push("");
                lines.push(run.ok ? run.text : `Codex failed: ${run.error}`);
                lines.push("");
            }
        } else {
            lines.push("Not run.");
        }
        lines.push("");
    }

    if (report.codexNotes?.length) {
        lines.push("## Codex Notes");
        lines.push("");
        for (const note of report.codexNotes) {
            lines.push(`### ${note.title}`);
            lines.push("");
            lines.push(note.text);
            lines.push("");
        }
    }

    return lines.join("\n");
}

function sumCodexTokens(results) {
    let input = 0;
    let output = 0;
    let total = 0;
    let hasAny = false;
    for (const item of results || []) {
        for (const codex of getCodexOutputs(item)) {
            if (codex.inputTokens !== null && codex.inputTokens !== undefined) {
                input += codex.inputTokens;
                hasAny = true;
            }
            if (codex.outputTokens !== null && codex.outputTokens !== undefined) {
                output += codex.outputTokens;
                hasAny = true;
            }
            if (codex.totalTokens !== null && codex.totalTokens !== undefined) {
                total += codex.totalTokens;
                hasAny = true;
            }
        }
    }

    return hasAny ? `${input} in / ${output} out / ${total || input + output} total` : "not reported";
}

function formatRunSummary(results) {
    const ollamaTimes = (results || [])
        .map(item => item.ollama?.elapsedMs)
        .filter(isFiniteNumber);
    const codexTimes = [];
    const codexByModel = new Map();

    for (const item of results || []) {
        for (const codex of getCodexOutputs(item)) {
            if (!isFiniteNumber(codex.elapsedMs)) continue;
            codexTimes.push(codex.elapsedMs);
            const modelTimes = codexByModel.get(codex.model) || [];
            modelTimes.push(codex.elapsedMs);
            codexByModel.set(codex.model, modelTimes);
        }
    }

    const parts = [];
    if (ollamaTimes.length) parts.push(`Ollama median ${formatMs(median(ollamaTimes))}`);
    if (codexTimes.length) parts.push(`Codex median ${formatMs(median(codexTimes))}`);
    for (const [model, values] of Array.from(codexByModel.entries()).sort(([a], [b]) => a.localeCompare(b))) {
        parts.push(`${model} median ${formatMs(median(values))}`);
    }

    return parts.join("; ");
}

function formatMedianCodexTokens(results) {
    const input = [];
    const output = [];
    const total = [];
    for (const item of results || []) {
        for (const codex of getCodexOutputs(item)) {
            if (isFiniteNumber(codex.inputTokens)) input.push(codex.inputTokens);
            if (isFiniteNumber(codex.outputTokens)) output.push(codex.outputTokens);
            const totalTokens = codex.totalTokens ?? (
                isFiniteNumber(codex.inputTokens) && isFiniteNumber(codex.outputTokens)
                    ? codex.inputTokens + codex.outputTokens
                    : null);
            if (isFiniteNumber(totalTokens)) total.push(totalTokens);
        }
    }

    if (!input.length && !output.length && !total.length) return "not reported";
    return `${formatNumber(median(input))} in / ${formatNumber(median(output))} out / ${formatNumber(median(total))} total`;
}

function median(values) {
    if (!values.length) return null;
    const sorted = [...values].sort((a, b) => a - b);
    const mid = Math.floor(sorted.length / 2);
    return sorted.length % 2 === 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
}

function isFiniteNumber(value) {
    return typeof value === "number" && Number.isFinite(value);
}

function formatNumber(value) {
    if (!isFiniteNumber(value)) return "?";
    return Number.isInteger(value) ? `${value}` : value.toFixed(1);
}

function getCodexOutputs(item) {
    if (item.codexRuns?.length) return item.codexRuns;
    return item.codex ? [item.codex] : [];
}

function formatTokens(codex) {
    const input = codex.inputTokens ?? "?";
    const output = codex.outputTokens ?? "?";
    const total = codex.totalTokens ?? (Number.isFinite(input) && Number.isFinite(output) ? input + output : "?");
    return `${input} in / ${output} out / ${total} total`;
}

function setTiming(rawMs, ollamaMs, codexMs, totalMs) {
    const boxes = $("#timing").children;
    boxes[0].querySelector("strong").textContent = formatMs(rawMs);
    boxes[1].querySelector("strong").textContent = formatMs(ollamaMs);
    boxes[2].querySelector("strong").textContent = codexMs === null || codexMs === undefined ? "manual" : formatMs(codexMs);
    boxes[3].querySelector("strong").textContent = formatMs(totalMs);
}

function renderResults(results) {
    resultsEl.innerHTML = "";
    for (const result of results) {
        const node = template.content.firstElementChild.cloneNode(true);
        const raw = result.candidate.raw;
        $(".js-title", node).textContent = `${raw.title}${raw.year ? ` (${raw.year})` : ""}`;
        $(".js-genres", node).textContent = raw.genres || "No genres";
        $(".js-score", node).textContent = raw.predictedScore ? `${raw.predictedScore}%` : "No score";
        $(".js-label", node).textContent = raw.predictedLabel;
        $(".js-reason", node).textContent = raw.predictedReason || "No explanation";
        $(".js-tags", node).textContent = raw.reasonTags || "No tags";
        $(".js-confidence", node).textContent = `${raw.affinityScore ?? "?"} / ${raw.confidenceScore ?? "?"}`;
        $(".js-factors", node).textContent = [
            "Positive factors:",
            ...(raw.positiveFactors.length ? raw.positiveFactors : ["none"]),
            "",
            "Negative factors:",
            ...(raw.negativeFactors.length ? raw.negativeFactors : ["none"]),
            "",
            "Warnings:",
            ...(raw.warningFactors.length ? raw.warningFactors : ["none"]),
            "",
            "Liked anchors:",
            ...(raw.similarLiked.length ? raw.similarLiked : ["none"]),
            "",
            "Disliked anchors:",
            ...(raw.similarDisliked.length ? raw.similarDisliked : ["none"])
        ].join("\n");

        $(".js-ollama-prompt", node).textContent = result.candidate.ollamaPrompt;
        $(".js-codex-prompt", node).textContent = result.candidate.codexPrompt;

        if (result.ollama) {
            $(".js-ollama-elapsed", node).textContent = `Elapsed: ${formatMs(result.ollama.elapsedMs)}`;
            $(".js-ollama-output", node).classList.remove("empty");
            $(".js-ollama-output", node).textContent = result.ollama.ok ? result.ollama.text : `Ollama failed: ${result.ollama.error}`;
            $(".js-ollama-raw", node).textContent = result.ollama.rawResponse || result.ollama.error || "";
        }
        if (result.codex) {
            $(".js-codex-elapsed", node).textContent = `Elapsed: ${formatMs(result.codex.elapsedMs)} · ${result.codex.model} · ${formatTokens(result.codex)}`;
            $(".js-codex-output", node).classList.remove("empty");
            $(".js-codex-output", node).textContent = result.codex.ok ? result.codex.text : `Codex failed: ${result.codex.error}`;
        } else if (result.codexRuns?.length) {
            $(".js-codex-elapsed", node).textContent = result.codexRuns
                .map(run => `${run.model}: ${formatMs(run.elapsedMs)}, ${formatTokens(run)}`)
                .join(" | ");
            $(".js-codex-output", node).classList.remove("empty");
            $(".js-codex-output", node).textContent = result.codexRuns
                .map(run => [
                    `### ${run.model}`,
                    `Elapsed: ${formatMs(run.elapsedMs)}`,
                    `Tokens: ${formatTokens(run)}`,
                    "",
                    run.ok ? run.text : `Codex failed: ${run.error}`
                ].join("\n"))
                .join("\n\n");
        }

        $(".js-copy-ollama", node).addEventListener("click", () => copyText(result.candidate.ollamaPrompt));
        $(".js-copy-codex", node).addEventListener("click", () => copyText(result.candidate.codexPrompt));
        resultsEl.appendChild(node);
    }
}

async function copyText(text) {
    await navigator.clipboard.writeText(text);
    statusEl.textContent = "Prompt copied.";
}

function escapeHtml(value) {
    return value.replace(/[&<>"']/g, char => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        "\"": "&quot;",
        "'": "&#039;"
    }[char]));
}

function labelCodexModel(name) {
    if (name === "gpt-5.4-mini") return `${name} (cheap baseline)`;
    if (name === "gpt-5.5") return `${name} (quality check)`;
    return name;
}

initialize();

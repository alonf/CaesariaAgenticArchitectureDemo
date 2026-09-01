// Renders the remediation workflow's Mermaid diagram (generated server-side from the code-built
// graph) into the Command Center panel. Mermaid is vendored locally; nothing loads from a CDN.
window.caesareaWorkflow = {
    render: async function (elementId, definition) {
        const element = document.getElementById(elementId);
        if (!element || !window.mermaid || !definition) {
            return;
        }

        const theme = document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "neutral";
        window.mermaid.initialize({ startOnLoad: false, theme: theme, securityLevel: "strict" });

        try {
            const { svg } = await window.mermaid.render(elementId + "-svg", definition);
            element.innerHTML = svg;
        } catch (error) {
            element.textContent = "The workflow diagram could not be rendered: " + error;
        }
    },

    // Projects the live run state onto the rendered diagram: each executor's node gets a
    // wf-running / wf-completed / wf-failed class. Nodes are matched by their label text
    // (the executor id, optionally suffixed by Mermaid, e.g. "validate (Start)") so the
    // mapping survives Mermaid's generated element ids.
    highlight: function (elementId, states) {
        const element = document.getElementById(elementId);
        if (!element) {
            return;
        }

        for (const node of element.querySelectorAll("g.node")) {
            node.classList.remove("wf-running", "wf-completed", "wf-failed");
            const label = (node.textContent || "").trim();
            const key = Object.keys(states || {}).find(id => label === id || label.startsWith(id + " "));

            if (key && states[key]) {
                node.classList.add("wf-" + states[key]);
            }
        }
    }
};

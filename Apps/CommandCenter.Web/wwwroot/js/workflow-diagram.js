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
    }
};

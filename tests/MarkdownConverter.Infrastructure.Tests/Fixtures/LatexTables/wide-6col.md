# Six columns with mixed content

Short identifiers and numbers next to one descriptive column.

| ID | Component | Owner | Status | Risk | Notes |
|---:|-----------|-------|:------:|-----:|-------|
| 1 | Parser | Infrastructure | Stable | 0.10 | Wraps the Markdig pipeline and exposes the parsed document to every converter. |
| 2 | Word export | Infrastructure | Stable | 0.25 | Produces native equations, tables, citations and callouts without requiring an Office installation. |
| 3 | LaTeX export | Infrastructure | Review | 0.55 | Shares one renderer with the PDF pipeline, so every table change has to be verified for both outputs. |
| 4 | PDF export | Infrastructure | Review | 0.60 | Runs the external compiler twice and reports the compiler log when the build fails. |
| 5 | Preview | Presentation | Beta | 0.35 | Renders the document in the browser and keeps the scroll position synchronised with the editor. |
| 6 | Desktop shell | Presentation | Beta | 0.40 | Hosts the shared web application and adds local file access and PDF compilation. |

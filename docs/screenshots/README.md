# PriceCompare Screenshot Guide

Add only real captures of the application or its generated report to this directory. Do not use generated or mock screenshots.

## Required captures

### `main-window.png`

Start PriceCompare and capture the main window with the OLD and NEW file areas and column-mapping controls visible. The sample files may be selected if that makes the mapping state clearer.

### `comparison-results.png`

Use `samples/price-old.csv` and `samples/price-new.csv`, then run **Compare**. Capture the summary and result DataGrid in one frame where possible. The expected summary is:

- Added: 1
- Removed: 1
- Changed: 3
- Unchanged: 4

### `excel-report.png`

Export the sample comparison and capture the opened XLSX report, showing either the `Summary` sheet or the `Changes` sheet.

## Optional capture

### `validation-issues.png`

Create a temporary local CSV with a duplicate SKU or malformed value, import it, and capture the validation-issues view. Do not add that temporary input file to `samples/` unless it becomes a maintained project fixture.

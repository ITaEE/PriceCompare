# PriceCompare

## Overview

PriceCompare is a local Windows desktop application for comparing two supplier or internal price lists in XLSX or CSV format.

The user independently maps the required SKU and Price columns plus optional Name and Stock columns for the old and new files. PriceCompare then compares rows by normalized SKU and reports added, removed, changed, and unchanged products.

The application is intended as a portfolio-grade example of practical C#/.NET automation work for real business data.

## Screenshots

Screenshots will be added after the final UI capture. The planned real captures and their reproducible states are listed in [docs/screenshots/README.md](docs/screenshots/README.md).

## Features

- XLSX / CSV import without Microsoft Excel
- independent column mapping for old and new files
- automatic suggestions for common column names
- case-insensitive, trimmed SKU comparison
- price change detection
- price difference and percentage calculation
- stock comparison
- duplicate SKU detection
- row-level validation issues
- result filtering
- search by SKU or product name
- Excel report generation
- fully local processing

## Demo

Run the included sample scenario with [samples/price-old.csv](samples/price-old.csv) and [samples/price-new.csv](samples/price-new.csv).

| Metric | Result |
| --- | ---: |
| Old rows | 8 |
| New rows | 8 |
| Added | 1 |
| Removed | 1 |
| Changed | 3 |
| Unchanged | 4 |
| Errors | 0 |
| Duplicates | 0 |

## Tech Stack

- C#
- .NET 8
- WPF
- MVVM
- async/await
- dependency injection
- ClosedXML
- CsvHelper
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- xUnit
- nullable reference types

Microsoft Office Interop, COM automation, external databases, cloud services, and unsafe code are not used.

## Architecture

PriceCompare is a small layered solution with separate domain, application, infrastructure, and UI concerns.

```text
PriceCompare.sln

src/
  PriceCompare.Core
  PriceCompare.Application
  PriceCompare.Infrastructure
  PriceCompare.Wpf

tests/
  PriceCompare.Tests

samples/
  price-old.csv
  price-new.csv
```

### PriceCompare.Core

Contains domain models, validation primitives, SKU normalization, comparison statuses, and the comparison engine.

### PriceCompare.Application

Contains application interfaces, column mapping suggestions, and the comparison workflow.

### PriceCompare.Infrastructure

Contains CSV/XLSX inspection and import implementations plus XLSX report generation.

### PriceCompare.Wpf

Contains the WPF UI, view models, commands, file-dialog service, and dependency-injection composition root.

Business comparison logic is not implemented in `MainWindow.xaml.cs`.

## Getting Started

Requirements:

- Windows 10/11
- .NET 8 SDK

No Microsoft Excel installation is required.

## Build

```powershell
dotnet restore
dotnet build --configuration Release
```

## Run

```powershell
dotnet run --project .\src\PriceCompare.Wpf\PriceCompare.Wpf.csproj
```

## Tests

```powershell
dotnet test --configuration Release
```

The solution contains 38 automated tests covering comparison statuses, price percentages, stock comparison, SKU normalization, duplicate handling, column mapping, CSV/XLSX import, cancellation, malformed input, and Excel report generation. File-based tests create their own temporary files and do not require Microsoft Excel.

## Usage

1. Select the old price list.
2. Select the new price list.
3. Review the automatically suggested column mapping.
4. Adjust SKU, Name, Price, or Stock mappings if needed.
5. Click **Compare**.
6. Filter the results by status or search by SKU/name.
7. Click **Export Report** to create an XLSX report.

Name and Stock mappings may be left empty. Stock changes are compared only when Stock is mapped for both files.

Duplicate SKU groups are reported and all rows belonging to the duplicated SKU are excluded from comparison so the result remains deterministic.

## Supported Input

Supported formats:

- `.xlsx`
- `.csv`

Required mappings:

- SKU
- Price

Optional mappings:

- Name
- Stock

SKU values are trimmed and compared case-insensitively.

## Input example

`samples/price-old.csv`:

```csv
SKU,Name,Price,Stock
A-1001,Wireless Mouse,24.90,18
A-1002,Mechanical Keyboard,79.00,12
```

`samples/price-new.csv` intentionally uses different column names:

```csv
ProductCode,ProductName,Price,Quantity
A-1001,Wireless Mouse,26.90,20
A-1002,Mechanical Keyboard,79.00,12
```

Map `SKU` / `ProductCode` as SKU and `Stock` / `Quantity` as Stock.

## Report explanation

The generated workbook contains:

- `Summary`
- `Changes`
- `Added`
- `Removed`
- `Errors`

Item sheets include SKU, Name, old/new prices, price difference, price difference percentage, and old/new stock values.

Headers use autofilter, the first row is frozen, numeric formats are applied, and columns are sized for readability.

## Privacy

PriceCompare works fully locally and does not send file data to the internet.

Application logging is limited to technical stages, row counts, issue counts, duplicate-group counts, file extension, and exception type. It does not intentionally log SKU values, product names, prices, file contents, or personal data.

## Limitations

- exactly two files are compared at a time
- comparison key is SKU only
- only XLSX and CSV are supported
- only the first XLSX worksheet is processed
- no history database is stored
- a malformed required price causes that row to be excluded
- malformed optional stock is reported and treated as missing
- when old price is zero, percentage change is left blank because a finite percentage is undefined
- duplicate SKU rows are reported and excluded from comparison

## Roadmap

Not implemented in the current version:

- comparison of more than two files
- configurable extra columns
- fuzzy product matching
- folder monitoring
- automatic processing
- PDF reports
- database history
- CLI mode
- ERP/API integrations

## Project Purpose

This project was built as a portfolio example of a production-style Windows automation tool using C#, WPF, Excel/CSV processing, validation, testing, and layered architecture.

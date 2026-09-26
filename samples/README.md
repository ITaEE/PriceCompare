# Sample comparison

Use `price-old.csv` as the old file and `price-new.csv` as the new file.

Suggested mappings:

| Logical field | Old file | New file |
|---|---|---|
| SKU | SKU | ProductCode |
| Name | Name | ProductName |
| Price | Price | Price |
| Stock | Stock | Quantity |

Expected summary:

- Old rows: 8
- New rows: 8
- Added: 1 (`A-1009`)
- Removed: 1 (`A-1006`)
- Changed: 3 (`A-1001`, `A-1003`, `A-1005`)
- Unchanged: 4
- Errors: 0
- Duplicates: 0

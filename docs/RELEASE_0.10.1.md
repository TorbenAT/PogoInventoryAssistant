# Release 0.10.1

## iPhone pretest gate correction

The real iPhone fixture run found 24 PNG files. Twenty-three decoded successfully and one failed. Version 0.10.0 rejected the complete pretest whenever any file failed, even though the requested minimum was 20 usable images.

Version 0.10.1 treats the iPhone folder as an exploratory cross-platform fixture set:

- at least `--min-images` files must decode successfully
- the decoded files must all be portrait screenshots
- at least two distinct decoded screenshots are required
- the successful decode rate must be at least 90 percent by default
- rejected files remain visible in JSON, Markdown, CSV and console diagnostics

For the reported result, 23 of 24 files is a 95.8 percent decode rate. The pretest can therefore continue while retaining the rejected file for later decoder investigation.

## New diagnostics

The command now prints each rejected file in this form:

```text
Rejected image: <file>: <error type>: <error detail>
```

The Markdown report also contains a dedicated rejected-images table.

## Safety impact

None. This release changes only the offline iPhone image pretest. It adds no Android input, no Calcy mechanism and no game automation.

## Expected CI

GitHub Actions must:

1. build all projects
2. pass all 86 self-tests
3. process the 24 committed iPhone files
4. accept the batch when at least 20 decode and the decode rate is at least 90 percent
5. print the exact rejected file and reason
6. upload the full diagnostic reports

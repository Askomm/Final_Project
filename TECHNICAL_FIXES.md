# Technical fixes

Upstream repository: https://github.com/Askomm/Final_Project
Base branch: main
Base commit: d6fe1d952e6cf34561d6bfe9169754d204c814b3

This package adds changes on top of that commit; it is not an unchanged export of it.
After applying and committing the patch, record your new `git rev-parse HEAD` value
in the final submission reports.

## Changes

- RentalDataLoader reuses BookingPeriod.Create: imported rentals must last 1–366 days.
- RentalOperations.ClosingTotal is used by both closing and loading. Stored closure
  totals must match the rate, billed dates and fee. Legacy missing/zero rates retain
  the standard-price fallback and missing fees remain zero.
- Menu option 4 adds a maximum total budget in KZT. It filters standard and discount
  offers independently, including service fees. Zero is valid; invalid input returns
  BUDGET_INVALID. Option 1 remains an unrestricted search. Fleet capacity is unchanged.
- RentalService.CloseDetailed computes late-return conflicts within the locked update.
  After a successful save, the menu lists affected active reservation IDs. It retains
  those reservations for staff resolution and does not automatically reschedule them.
- Nine regression tests cover imported boundaries, stored totals, legacy data,
  failure preservation, budget parsing/filtering/menu behavior, and conflict warnings.

## Verified results

Windows x64, .NET SDK 10.0.401: Release build, zero warnings and zero errors.
82 tests passed, zero failed. See Reports/technical-fixes-test-results.txt.

Existing DOCX/PDF reports are preserved and describe the previous 73-test version.
Update their test counts, budget-search description, conflict-warning behavior and
final commit identifier before submission. Reports/test-results.txt is the earlier run;
the technical-fixes log is the new run.

If an existing JSON file now fails validation, inspect the reported data problem.
Do not reset it or invent payment values to bypass validation; correct it from
the actual rental records and preserve a backup first.

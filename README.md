# Car Rental System — Final Project

C# console application for searching available cars, creating rentals, and recording returns.
Azamat Kuanyshpayev, IT-2514.
Three meaningful workflows and two reports.


## Build, run, test

Supported platform: **Windows x64**, **.NET 10 SDK** (validated with **10.0.401**).
No external NuGet packages and no database are required.
Open PowerShell in the folder containing `Code.sln`:

```powershell
dotnet build Code.sln -c Release --disable-build-servers -m:1
dotnet run --project Code.csproj -c Release --no-build
dotnet run --project Code.Tests/Code.Tests.csproj -c Release --no-build
```

The last command runs the deterministic assertion-based test executable.
Use it instead of `dotnet test`. It returns exit code 1 on a failing assertion.
Verified result: **82 passed, 0 failed**; build: **0 warnings, 0 errors**.
See `Reports/technical-fixes-test-results.txt` for actual execution output.

The repository contains the native Windows x64 DLL. To rebuild it, install
Visual Studio C++ Build Tools (tested with MSVC 19.51) and run:

```powershell
cmd /c Native\build-native.cmd
```

The C source exports `rental_service_fee`; `NativeFee` invokes it using Cdecl
P/Invoke. Its result, 250 KZT per booked day, is used in displayed quotes and
saved booking prices. The project copies the DLL beside the managed executable.

## Menu and input

```text
1. Search cars
2. Create rental
3. Close rental
4. Search within budget
0. Exit
```

Dates use **yyyy-MM-dd**, for example **2026-12-10**.
Enter 0 at the menu to stop normally. EOF at any prompt also exits normally.
Malformed input, invalid dates, missing IDs, unavailable cars and repeated closes
produce a clear Result error and return to the menu. The program does not keep
asking for the remaining fields after a parsing failure.

1. **Search cars:** start date, end date. Displays cars with no overlapping
   bookings, standard and 10%-discount quotes, and the fleet summary.
   No customer ID is needed. An empty result is successful and says no cars are available.
2. **Create rental:** customer ID, car ID, start date, end date.
   Checks customer eligibility, car existence and availability, calculates the
   native fee, allocates a unique ID, and saves immediately.
3. **Close rental:** rental ID, return date, payment method.
   Accepts **Cash**, **Card**, or **Transfer**, ignoring case and outer whitespace.
   Records the actual return date, normalized method and total paid in JSON.
   Closing an unknown or already-closed rental fails without changing the file.

Existing IDs are printed at startup. The seed has customers 1–3 and cars 1–5.
The original booking is rental 1, car 3, 2026-10-13 to 2026-10-17.

### Reproducible demonstration

Use a separate copy so a demonstration does not alter the seed:

```powershell
Copy-Item Data/rental-data.json demo-rentals.json
dotnet run --project Code.csproj -c Release --no-build -- --data demo-rentals.json
```

- Choose 1, enter 2026-12-10 and 2026-12-12. Car 1 costs 30,500 KZT
  standard (15,000 × 2 + 500) or 27,500 KZT with the comparison discount.
- Choose 2, customer 1, car 1, the same dates. The new rental ID is 2.
- Search again: car 1 is absent. A second overlapping booking fails.
- Choose 3, rental 2, return 2026-12-11, Card. It closes for 30,500 KZT.
- Search 2026-12-11 to 2026-12-12: car 1 is available again.
- Exit and restart with the same `--data` path: the closed record remains.
- Enter a word as an ID or an impossible date to show error recovery.

## Business rules

- Periods are **[start, end)**. End must be after start; maximum 366 booked days.
  A booking ending on December 12 does not block one starting on December 12.
- Customers need a driving license and age at least 18. Luxury cars require 21.
  Transmission preference is advisory in the new workflow: the selected car may
  have either transmission. The legacy customer-filtered quote API remains for
  earlier assignments and its existing tests.
- New bookings use the **standard** price. The discount report is a comparison,
  not an automatically applied discount.
- Daily prices: Default 15,000; Comfort 20,000; Luxury 30,000 KZT.
  Automatic transmission adds 5,000. The native service fee is charged once.
- An early return releases unused days but does not refund the agreed rental term.
  A late return adds the saved daily rate for extra days. The original service fee
  is unchanged. Same-day return is accepted with the full booked charge.
- Closed rentals preserve history: their occupied interval ends at the actual
  return date. Historical occupied dates remain unavailable; future dates are free.
- A late return is recorded even if it overlaps another existing reservation.
  The later reservation is retained for staff resolution; there is no automatic
  cancellation or rescheduling in this version.
- Dates are not compared with the system clock, allowing reproducible historical
  demonstrations. The app is a date-based booking ledger, not a live vehicle tracker.
- IDs increase from the maximum existing rental ID, including closed rentals.
  Exhausting the integer range produces a Result failure.

## JSON persistence and failures

Default path: **Data/rental-data.json relative to the current working directory**.
It is the live file; successful bookings and closes update it. Rebuilding the app
does not replace it with the copy in `bin`. Use `--data <path>` for a separate file.
The file must already exist; missing or malformed files are never silently reset.

A single JSON document contains `cars`, `customers` and `rentals`.
Each rental retains its original dates and IDs, saved `dailyPrice` and
`serviceFee`. `closure: null` means active; a closure object contains
`returnDate`, `paymentMethod` and `totalPaid`.
Older records without the new optional fields load as active; a legacy zero rate
uses the car's standard price on closing and retains its zero legacy service fee.

The loader checks required fields, real dates, enums, positive unique IDs,
references, nonnegative ages/prices, closure values and overlapping active bookings.
It returns read/parse failures without partial data.

Each update holds an exclusive `.lock` file handle while it reloads, validates,
calculates and saves. The new snapshot is written to a temporary file in the same
directory, flushed and closed, then replaces the live file. A failed save leaves
the prior JSON intact. Readers and writers are disposed using `using`; temporary
files are cleaned in `finally`. The empty lock file may remain, but its existence
does not mean a running instance owns the lock. Only cooperating app instances
are coordinated; manual external edits are outside that protocol.

The app retains closed rentals indefinitely for audit and ID uniqueness.
It is a local learning project without authentication, real payment processing,
encryption, deletion, customer management, cancellation or automatic backups.

## Functional programming evidence

| Requirement | Used implementation |
| --- | --- |
| Related immutable models | Car, Customer, Rental, RentalClosure records in Models.cs |
| Validated construction | BookingPeriod.Create with a private constructor |
| Pure core and safe transitions | RentalOperations.Create and Close return new snapshots; repeat close is rejected |
| Three named stages | SelectAvailable → PriceOffers → BuildSearchReport in RentalOperations.Search |
| Higher-order pricing and two policies | PriceOffers accepts a Func; CreatePrice(0m) and CreatePrice(.10m) produce different totals |
| Currying and partial application | ReportQueries.CreatePrice: discount → car → price |
| Explicit errors and composition | Result.Map/Bind in ConsoleApplication, RentalService, RentalOperations and RentalStore |
| Missing lookups | Option.Some/None and ToResult for customers, cars and rentals |
| Pattern matching with payloads | Result success/failure, Option, customer eligibility and RentalClosure |
| Two custom extensions | WithinBudget and SummarizeFleet in Reports.cs, used by the search reports |
| Two LINQ reports | Available-price comparison and fleet daily revenue potential |
| LINQ operations | Availability filtering, projection, price/name/ID tie-breakers, GroupBy, Count, Sum |
| Lazy processing | CreateOffers uses yield return; ToArray materializes once per pricing configuration |
| Side-effect separation | ConsoleApplication for terminal I/O; RentalStore/Loader for files; NativeFee for P/Invoke |
| Native integration | Real C service fee used for quotes and new bookings; actual and controlled-failure tests |
| Persistence and resource ownership | Atomic JSON replacement and scoped lock/file streams |
| Automated tests | 44 retained tests plus 38 final-project tests; pure, workflow, file and console checks |

Search reports use actual loaded fleet and booking data, including changes made in
earlier menu iterations. Fleet potential groups **all** cars by type and sums
their standard daily rates; it is capacity potential, not actual rental revenue.
Empty, no-match and tied-order cases are tested. Deferred calculations perform no
I/O; materialized arrays can be reused without repeating file or native calls.

## Files and tests

- `ConsoleApplication.cs`: menu loop and immediate input-error recovery.
- `RentalOperations.cs`: validated periods, parsing, availability, pricing pipeline,
  eligibility and immutable create/close transitions.
- `RentalStore.cs`: persistence transaction and application service.
- `RentalDataLoader.cs`: validated JSON reading and operational errors.
- `Models.cs`, `Result.cs`, `Reports.cs`, `NativeFee.cs`: domain and functional building blocks.
- `Code.Tests/FinalProjectTests.cs`: 38 tests, including two persistent workflows,
  failed replacement, lock contention, malformed closure, restart, EOF and menu errors.
- `Code.Tests/Program.cs`: 44 retained tests covering pure functions, factories,
  Map/Bind, Option, reports, laziness, resource release and native integration.
- `Reports/Project_Report.docx`: Project_Report in word format.
- `Reports/Individual_Report.docx`: Individual_Report in word format.
- `Reports/Project_Report.pdf`: Project_Report in pdf format.
- `Reports/Individual_Report.pdf`: Individual_Report in pdf format.


Controlled native failure (choose Search or Create, then enter valid inputs):

```powershell
dotnet run --project Code.csproj -c Release --no-build -- --demo-native-failure
```

The error returns to the menu; enter 0 to exit. No rental is saved.
`--demo-missing-file` and `--demo-malformed-file` are one-shot legacy diagnostic
commands that print the expected failure and exit with status 1.

## Repository

Repository: https://github.com/Askomm/Final_Project

## References

- Microsoft Learn: C# records, pattern matching, LINQ, System.Text.Json, and native
  interoperability. The project uses .NET framework APIs and its own C code;
  it has no added third-party runtime dependencies.




## Technical fixes

See [TECHNICAL_FIXES.md](TECHNICAL_FIXES.md) for the base commit, changed behavior,
validation rules, new tests, and report-update notes. Option 4 asks for dates and
a nonnegative maximum total budget (KZT). Option 1 remains unrestricted.
Late returns now display the IDs of affected active reservations after saving.

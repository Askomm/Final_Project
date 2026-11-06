# Car Rental System - Assignment 8

Student: **Azamat Kuanyshpayev**, group **IT-2514**. Individual submission.
This extends the supplied Assignment 7 project in the same application. Earlier
pipeline, validated requests, Result/Option, parsing and tests are retained.
Submission scope: rental reports, extensions, configured pricing, deferred offers,
C service-fee library, adapter, file export, tests and documentation. The Assignment 8
extension was prepared with AI assistance; this identifies project scope rather than
claiming unaided authorship. No additional team members are claimed.

## Build, run and test

Supported platform: **Windows x64**. Tested on Windows build 10.0.26300,
.NET SDK **10.0.401**, targeting **net10.0**. Install a .NET 10 SDK for x64.
There are no external NuGet packages. A compiled x64 native DLL is included, so
Visual Studio is needed only to rebuild the C component.

Extract the ZIP. Open PowerShell **inside `IT-2514_Kuanyshpayev_Azamat_Assignment8`**
(the folder containing `Code.sln`). Run:

```powershell
dotnet build Code.sln -c Release --disable-build-servers -m:1
dotnet run --project Code.csproj -c Release --no-build
dotnet run --project Code.Tests/Code.Tests.csproj -c Release --no-build
```

The test project is an assertions-based executable, not a `dotnet test` framework.
A failed assertion is reported as FAIL and gives exit code 1; success gives exit code 0.
Latest result: **44 passed, 0 failed**, including a real native DLL call. Build:
**0 warnings, 0 errors**. 
Single-worker build avoids build-server/worker startup problems in restricted hosts.

Running `dotnet run` asks for the customer ID, start date and end date by default.
No interactive flag is needed. The budget remains 60,000 KZT. For the documented
example, enter these three values when prompted:

```text
2
14-10-2026
16-10-2026
```

The program prints both reports and saves `rental-reports.json` in the working
folder. The file is generated on demand and excluded from the submission.

Safe failure demonstrations (each is expected to exit with code 1):

```powershell
dotnet run --project Code.csproj -c Release --no-build -- --demo-missing-file
dotnet run --project Code.csproj -c Release --no-build -- --demo-malformed-file
dotnet run --project Code.csproj -c Release --no-build -- --demo-native-failure
```

Expected codes respectively: DATA_FILE_MISSING, DATA_FILE_MALFORMED,
NATIVE_UNAVAILABLE. The native failure demonstration asks for customer ID and dates first; enter the
three example values above. The native failure flag injects a loading exception; the normal
run always uses the actual C DLL. A failed workflow does not export or change a
previous report. An export failure returns REPORT_EXPORT_FAILED and a nonzero exit.

## Input and domain rules

`Data/rental-data.json` supplies 5 cars, 3 customers and 1 existing rental.
`Data/malformed.json` supports the malformed-file demonstration. Earlier text files
and parsers are kept for continuity. Data is loaded once and validated before use:
arrays and fields must be present, IDs positive and unique, enums valid, rental
references known, and end dates later than start dates. Negative ages are invalid.
A stored underage/unlicensed customer can exist but cannot make an eligible request.

Dates use dd-MM-yyyy at the console. Periods are half-open [start, end), so a car
becomes available on an existing rental's end date. Customers must be licensed and
at least 18; luxury cars require age 21. Transmission must match the customer's
preference. Overlapping rentals exclude a car. Invalid requests remain explicit
Result failures. Service-fee support limits the integrated workflow to 1..366 days.

Base daily prices in KZT: Default 15,000, Comfort 20,000, Luxury 30,000; automatic
transmission adds 5,000. Configured discounts apply to the daily rental price;
rounding is to 2 decimal places, midpoint away from zero. The fixed native service
fee is added once per offer and is not discounted.

## Two reports and expected results

1. **Affordable rental offers**, for a customer comparing available cars. Input:
   JSON cars/rentals/customer, validated dates, a budget and configured pricing.
   Filters: eligibility, transmission, no overlap, and total <= budget. Output:
   car ID/name, daily price, days, service fee and total. Order: total ascending,
   ordinal car name, then ID. Standard and discount are two configurations of this
   one report. For the example input, IDs **4 then 2** each cost **50,500 KZT**
   standard or **45,500 KZT** discounted: 25,000 * 2 + 500, or 22,500 * 2 + 500.
   A budget with no matches produces an empty report; no available cars is a domain
   failure from the retained rental workflow.
2. **Fleet daily revenue potential**, for a fleet manager. Input: all validated
   JSON cars and the standard pricing function. There is intentionally no
   availability filter: this estimates capacity for the whole fleet, not booked
   revenue. Group by CarType, count cars and sum daily rates; order by potential
   descending, then category enum value. Output: category, count, KZT/day.
   Expected: **Comfort / 3 / 75,000**, **Luxury / 1 / 35,000**,
   **Default / 1 / 15,000**. Total potential is **125,000 KZT/day**.

Both pure report functions accept empty sequences. `Where`, `Select`, `OrderBy`,
`OrderByDescending`, `ThenBy`, `GroupBy`, `Count` and `Sum` are used across the
reports. Query transformations are in `Reports.cs`; console formatting is in
`Program.cs`, export in `ReportExporter.cs`, and native loading in `NativeFee.cs`.

## Requirement map

| Task | Implementation |
| --- | --- |
| 1 Extensions | `RentalExtensions.WithinBudget` and `SummarizeFleet`, called by report queries |
| 2 LINQ reports | `ReportQueries.AffordableOffers`, `FleetReport`; `ReportWorkflow.Run` integrates both |
| 3 Currying | `ReportQueries.CreatePrice(discount)(car)`; Program fixes 0 and 0.10, passes both functions into workflow |
| 4 Deferred execution | `CreateOffers` uses yield return; workflow materialises with ToArray; timing test uses a test-only counter |
| 5 Disposal | loader's File.ReadAllText; export's bounded using StreamWriter; cleanup tests |
| 6 Retention | resource note below |
| 7 C function | `Native/rental_native.c` and `.h`: rental_service_fee |
| 8 Binding/build | `Native/build-native.cmd`, `NativeFee`, runtime DLL copy in Code.csproj |
| Earlier safe pipeline | `Pipeline.cs`, `RentalRequest.cs`, `RentalWorkflow.cs`, `Result.cs`, `RentalDataLoader.cs` |

`WithinBudget`: receiver IEnumerable<Offer>, result IEnumerable<Offer>, lazy
inclusive budget filter. Budget belongs to a sequence of offers because each offer
already contains the complete customer cost. The workflow rejects negative budgets.
`SummarizeFleet`: receiver IEnumerable<Car>, result IEnumerable<FleetTotal>, lazy
group projection using a supplied price function. Category and capacity belong to
the fleet collection, independent of display or storage. Neither extension mutates
its source or merely renames a standard method.

`CreatePrice` is decimal -> (Car -> decimal); calling it once fixes the discount.
On the same Comfort automatic car, the two functions return **25,000** and
**22,500**. A closure captures only the immutable discount; no global configuration
is changed. Both functions are passed into the report workflow and used on the same
available-car snapshot.

## Native contract and reproducible rebuild

Compiler used: **MSVC C/C++ 19.51.36260 for x64**, toolset directory 14.51.36231,
Visual Studio 2026 Community. Prerequisite to rebuild: Visual Studio or Build Tools
with Desktop development with C++ (MSVC x64 tools and Windows SDK).
From the project root:

```powershell
.\Native\build-native.cmd
dotnet build Code.sln -c Release --disable-build-servers -m:1
dotnet run --project Code.csproj -c Release --no-build
dotnet run --project Code.Tests/Code.Tests.csproj -c Release --no-build
```

The script discovers Visual Studio with vswhere, initializes vcvars64, and runs:

```text
cl /nologo /W4 /WX /O2 /MT /LD rental_native.c /Fobuild\rental_native.obj /link /OUT:..\runtimes\win-x64\native\rental_native.dll /IMPLIB:build\rental_native.lib
```

Compiler working directory: `Native`. `.c` selects C compilation. `/MT` uses the
static C runtime; `/LD` emits a DLL; warnings are errors. Intermediates go in
`Native/build`, excluded from the archive.

Export: `int32_t __cdecl rental_service_fee(int32_t days)`.
Valid input: **1..366 days**. Output: nonnegative **whole KZT**, exactly days * 250.
Invalid input: **-1**. Maximum output: 91,500; no overflow within the domain.
No floating-point math or rounding is involved. Manually checked example:
**2 days * 250 KZT/day = 500 KZT**. No memory or handles cross this boundary.
The header uses fixed-width int32_t; C# uses 32-bit int for argument and result,
DllImport, exact export spelling, and CallingConvention.Cdecl. No unsafe code or
special marshalling settings are needed; both projects target x64.

The included DLL is in `runtimes/win-x64/native/rental_native.dll`. The project links
it as `rental_native.dll` and copies it beside the managed assembly on build, also
into the referencing test project's output. The runtime resolves it there. The
adapter validates days before calling and translates DllNotFoundException,
EntryPointNotFoundException and BadImageFormatException to NATIVE_UNAVAILABLE.
A native -1 maps to NATIVE_REJECTED. No managed fallback replaces the real call.

Integration difficulty encountered: the C compiler was installed but absent from
the ordinary shell PATH. The build script now discovers the installation and loads
the x64 compiler environment before compilation. This also ensures the DLL matches
the x64 managed process. Tests inject loader exceptions rather than delete an
already-loaded library, avoiding misleading results caused by native module caching.
Reference: [Microsoft P/Invoke documentation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke).

## Resource ownership, retention and allocation

The loader uses File.ReadAllText, which owns and closes its internal stream before
returning the text, including on read failure. JSON deserialization happens after
that scope. Loader tests open files exclusively and delete them after both success
and malformed content to check observable handle cleanup.

`ReportExporter.Save` owns its StreamWriter in a bounded using block. The writer
is disposed on success or write failure, before the temporary file is moved. A move
failure still runs finally cleanup; the existing destination remains intact.
Serialization finishes before the writer is opened. Tests check exclusive access
on success and removal of temporary output after a move failure. Cleanup is best
effort if the OS itself refuses deletion; the original failure remains the Result.
There is no open resource inside the yield iterator.

The loader materialises read-only snapshots of JSON arrays. The reporting workflow
materialises available cars once and each configured offer sequence once with
ToArray, then materialises sorted report rows for display/export. This is a small
bounded dataset; the retained Assignment 7 quote comparison creates some additional
short-lived lists in exchange for preserving the validated earlier pipeline.
The budget query could be moved before materialisation for large fleets, but a
snapshot is intentional here for predictable reuse.

Before ToArray, enumerating CreateOffers again repeats pure price calculations and
retains its source and price delegate while the iterator remains referenced. It
never rereads a file or changes rental state. After materialisation, repeated use
of the resulting array reuses values without invoking pricing again. The test probe
shows counts 0 at creation, 1 after First, 6 after materialising all five again,
6 after repeated snapshot use, and 11 after re-enumerating the lazy source.
Production pricing has no diagnostic side effects.

Streaming lowers retained result memory but repeats work if reused. A snapshot
stores O(n) values and gives stable repeated access. Sorting and grouping themselves
need buffering. Closures capture only a discount decimal; the iterator temporarily
retains its car sequence. There are no growing static collections, caches, events,
or background subscriptions. Static JSON options are bounded configuration.
No claim is made that a memory leak was fixed. The native function allocates no
memory; P/Invoke manages the module lifetime. Garbage collection is not used as a
substitute for deterministic file disposal.

## Automated test inventory

`Code.Tests/Program.cs` contains 20 retained tests and 24 new tests. At least eight
are pure/domain tests; complete success and failure workflows include JSON input,
both reports, native integration and export gating. Missing/malformed file tests
use unique paths with finally cleanup. A8 temporary directories are under the
working folder so the suite works in restricted workspaces.

| # | Case | Expected |
| --- | --- | --- |
| 1 | Normal workflow | Two eligible cars; each 50,000 KZT. |
| 2 | Empty dataset | Empty input produces zero quotes. |
| 3 | Age 18 boundary | Licensed age 18 can rent a regular car. |
| 4 | Invalid and missing input | Missing values, numeric enums, underage row and invalid dates rejected. |
| 5 | Two pricing policies | Standard 50,000 vs discounted 45,000. |
| 6 | Stages and input collections stay unchanged | All stages agree and input arrays are unchanged. |
| 7 | Factory success is used by the validated workflow | Validated request flows into two correct quotes. |
| 8 | Factory rejects missing customer and invalid dates | Missing customer, nonpositive ID, invalid dates and underage customer rejected. |
| 9 | Pattern matching handles both result cases and eligibility rules | Correct success/error cases and no-license rejection. |
| 10 | One-day boundary, repeated operation, and state preservation | One day costs 25,000; repeated operations preserve data. |
| 11 | Assignment 7 integrated Result workflow succeeds | Result chain produces standard and discounted quotes. |
| 12 | Assignment 7 reports distinct domain failures | Distinct ID, customer, period and availability errors. |
| 13 | Option lookup distinguishes present and absent customers | Some/None distinguish absence from false license. |
| 14 | Map transforms a successful value | Map gives Value: 42. |
| 15 | Bind continues with the next Result operation | Bind gives 12 and propagates next-step failure. |
| 16 | Map and Bind propagate failure without invoking functions | Neither callback runs after failure. |
| 17 | Valid JSON file loads as one complete dataset | JSON contains 5 cars, 3 customers, 1 rental. |
| 18 | Missing input file becomes an explicit failure | DATA_FILE_MISSING with cleanup. |
| 19 | Malformed JSON becomes a failure without partial data | DATA_FILE_MALFORMED with cleanup. |
| 20 | Result cases reject a missing payload | Null success/error payloads rejected. |
| 21 | A8 budget extension includes boundary and excludes expensive offers | 30,500 budget includes car 1; zero budget has none. |
| 22 | A8 fleet extension groups and sums | Comfort count 3 / 75,000; fleet total 125,000. |
| 23 | A8 both curried functions differ on same input | Same car gives 25,000 and 22,500. |
| 24 | A8 invalid configuration rejected | Discounts outside 0..1 rejected. |
| 25 | A8 both reports accept empty input | Both reports empty. |
| 26 | A8 reports with no matches | Filtered reports empty. |
| 27 | A8 deterministic tied offer ordering | Name then ID resolves equal totals. |
| 28 | A8 deterministic tied fleet ordering | Category resolves equal potential. |
| 29 | A8 deferred timing and snapshot reuse | Probe counts 0, 1, 6, 6, 11. |
| 30 | A8 real native call matches manual fee | Real DLL: 2 -> 500, 1 -> 250, 366 -> 91,500. |
| 31 | A8 adapter rejects days before calling native | Invalid days rejected with zero calls. |
| 32 | A8 native binding failures become Result failures | Three loading/binding failures and native -1 translated. |
| 33 | A8 complete JSON to reports native and export workflow | JSON to real native reports: 50,500 / 45,500; fleet totals; export parsed and unlocked. |
| 34 | A8 complete failure workflow preserves data and previous export | Native failure preserves input and previous output. |
| 35 | A8 export failure cleans temporary file and releases writer | Failed move removes temporary output. |
| 36 | A8 input file handles released after success and parsing failure | Exclusive open/delete works after valid and malformed load. |
| 37 | A8 duplicate and dangling data rejected | Duplicate IDs, dangling references, bad dates/enums rejected. |
| 38 | A8 budget and excessive period failures | Negative budget and >366-day fee period rejected. |
| 39 | Missing car fields | Each omitted car constructor field returns DATA_FILE_MALFORMED. |
| 40 | Missing customer fields | Each omitted customer constructor field returns DATA_FILE_MALFORMED. |
| 41 | Missing rental fields | Each omitted rental constructor field returns DATA_FILE_MALFORMED. |
| 42 | Explicit zero/false values | Stored values are preserved; ineligible customer still cannot rent. |
| 43 | Dependent workflow operations skipped | Unknown customer produces zero native/pricing calls. |
| 44 | Actual application failure processes | All three failure demos exit 1 and preserve existing report bytes without extra files. |

JSON validation uses `RespectRequiredConstructorParameters = true` (.NET 10).
Every positional record constructor parameter is required: an absent enum, boolean,
age, or date cannot silently become its CLR default. Explicit zero/false values
remain valid stored values where the domain permits them. Missing-field tests remove
each field individually and assert DATA_FILE_MALFORMED. The process-level test runs
the actual application entry point, so it verifies the real export guard rather
than reproducing that guard inside the test. It uses the installed dotnet host,
redirected input/output, a 15-second timeout, and isolated temporary output folders.




# Assignment 7 - Exception Handling in Functional Programming

## Project information

- **Project:** Console-based Car Rental System
- **Student:** Azamat Kuanyshpayev
- **Group:** IT-2514
- **Work format:** Individual work
- **Target framework:** .NET 10 (`net10.0`)
- **Test approach:** Included assertions-based console test runner; no xUnit or third-party test framework

## 1. Project workflow

The application loads cars, customers, and existing rentals together from `Data/rental-data.json`. The user enters a customer ID and a rental period. The workflow looks up the customer, validates the request, finds available cars, calculates Standard and 10% Discount quotes, displays the result, and saves a JSON output file.

A normal demonstration uses:

- Customer ID: `2`
- Start date: `14-10-2026`
- End date: `16-10-2026`

This sample period overlaps the existing rental for car 003, so cars 002 and 004 should be quoted. Each Standard quote is **50,000 KZT** for two days and each discounted quote is **45,000 KZT**. The dates are fixed sample data for a repeatable classroom demonstration; the application does not enforce a rule that rental dates must be in the future.

## 2. SDK and exact commands

Install the **.NET 10 SDK (10.0.x)**. Extract the ZIP, open a terminal, and run the following commands from the extracted project root, the folder containing `Code.sln` and `Code.csproj`:

```bash
dotnet --version
dotnet restore Code.sln
dotnet build Code.sln
dotnet run --project Code.Tests/Code.Tests.csproj
dotnet run --project Code.csproj
```

The last command runs the interactive application. The test project is an executable test runner, so run it with `dotnet run` rather than `dotnet test`. It prints `PASS` or `FAIL` for each group and exits with a non-zero code if one or more groups fail.


## 3. Input files and file-boundary demonstrations

`Data/rental-data.json` is the normal input file. Its three required arrays are `cars`, `customers`, and `rentals`. Enum values use their names, such as `"Comfort"` and `"Automat"`; rental dates use ISO format `yyyy-MM-dd` in this JSON file.

The application also provides two command-line demonstrations. Run each from the project root:

```bash
dotnet run --project Code.csproj -- --demo-missing-file
dotnet run --project Code.csproj -- --demo-malformed-file
```

Expected outcomes:

| Demonstration | Expected outcome |
|---|---|
| Normal data file | `RentalDataLoader.Load` returns `Success` with all three collections. |
| `--demo-missing-file` | Prints `DATA_FILE_MISSING` and reports the missing path. No quote workflow starts. |
| `--demo-malformed-file` | Prints `DATA_FILE_MALFORMED` and reports that JSON parsing failed. No quote workflow starts. |

The failure demonstrations intentionally exit with a non-zero process code, because they show detected failures rather than successful quote generation. `Data/malformed.json` is a small deliberately invalid JSON example used only by that demonstration.

The application writes successful quotes to `rental-quotes.json` in the current working directory. It writes to a temporary file first and replaces the final file only after the temporary write completes.

## 4. Domain rules and expected outcomes

- IDs must be positive integers, and the customer must exist.
- A customer must be at least 18 years old and have a driving license. A customer must be at least 21 to rent a Luxury car.
- Rental dates entered in the console must use `dd-MM-yyyy`; the end date must be later than the start date.
- A car must match the customer's preferred transmission and must not have an overlapping rental.
- Daily base prices are Default = 15,000 KZT, Comfort = 20,000 KZT, and Luxury = 30,000 KZT. Automat transmission adds 5,000 KZT per day. The discounted policy applies 10% off.

Expected domain failures are returned as explicit `Result.Failure` values, with a code and a useful message:

| Code | Meaning / expected message |
|---|---|
| `CUSTOMER_ID_INVALID` | The entered customer ID is missing, malformed, or not positive. |
| `CUSTOMER_NOT_FOUND` | No customer with the requested ID exists. |
| `CUSTOMER_NOT_ELIGIBLE` | The customer is under 18 or does not have a driving license. |
| `RENTAL_PERIOD_INVALID` | The end date is not later than the start date. |
| `RENTAL_DATES_REQUIRED_OR_FORMAT` | One or both entered dates are missing or not in `dd-MM-yyyy` format. |
| `NO_CARS_AVAILABLE` | The request is valid, but no car matches the rules for the selected period. |

Operational file errors use separate codes, including `DATA_FILE_MISSING`, `DATA_FILE_MALFORMED`, `DATA_FILE_ACCESS_DENIED`, and `DATA_FILE_READ_FAILED`.

## 5. Assignment 7 implementation map

### Task 1 - Explicit domain outcomes

- `Result.cs` defines `Result<T, E>` with exactly two cases: `Success(Value)` and `Failure(Error)`. Each case rejects a null required payload, and callers can only construct one case at a time through `Ok` or `Fail`.
- `RentalWorkflow.cs` returns meaningful failures for invalid customer IDs, missing customers, ineligible customers, invalid rental periods, and requests with no available cars. These expected business failures do not use exceptions as normal control flow.
- `RentalError` in `RentalDataLoader.cs` carries an error code and message.

### Task 2 - Explicit missing values

- `Option<T>` in `Result.cs` defines `Some(Value)` and `None`.
- `RentalWorkflow.FindCustomer` returns `Some(customer)` or `None`; absence is not represented by a fabricated customer or `default(T)`.
- The main chain converts `None` into `CUSTOMER_NOT_FOUND` with `Option.ToResult`.
- A customer with `HasDrivingLicense == false` is still a present customer (`Some`); the `false` value triggers an eligibility failure rather than being treated as missing.

### Task 3 - Map and Bind composition

- `Result<T, E>.Map` transforms the successful payload and preserves a failure unchanged.
- `Result<T, E>.Bind` calls the next operation only after success and propagates a failure unchanged.
- The active chain in `RentalWorkflow.SearchQuotes` parses the ID, performs the optional lookup, converts the Assignment 6 factory outcome into a `Result`, finds available cars, and maps those cars into the Standard and Discounted quote comparison.
- Tests verify successful `Map` and `Bind` calls and use a counter to prove that neither supplied function runs after a failure.
- `Program.cs` handles the final workflow result explicitly with pattern matching.

### Task 4 - Exception handling at the file boundary

- `RentalDataLoader.Load` reads JSON outside the pure pricing and selection operations.
- Missing files/directories, malformed JSON, access denial, and other I/O errors become clear `Result.Failure` values with the input path in the message.
- The loader parses and validates the whole document in local variables, and returns `RentalData` only after all three arrays pass structural checks. A malformed file does not partially replace an existing dataset.
- `Program.cs` handles a load failure at the application boundary, prints the error, and does not begin the quote workflow.

### Task 5 - Integration

- Normal workflow: load sample JSON, submit a valid customer and date range, calculate both pricing policies, display quotes, and write the JSON output.
- Domain failure paths: invalid ID, missing customer, ineligible customer, invalid rental period, and no available cars.
- Lookup paths: customer present and customer absent; a stored `false` driving-license value remains a present customer.
- File failure paths: `--demo-missing-file` and `--demo-malformed-file`.
- The existing `Pipeline` stages and `RentalRequestFactory` from Assignment 6 remain in the same project and are used by the Result-based workflow.

### Different meanings of the result models

`RentalRequestCreationResult.Valid/Invalid` from Assignment 6 describes whether the factory could create a validated rental request. The new generic `Result<T, E>` composes operations throughout Assignment 7 and can represent validation errors, missing lookup outcomes after conversion, business-rule rejections such as `NO_CARS_AVAILABLE`, and file-boundary errors. A valid request does not guarantee a car can be quoted; the availability step can still return a separate workflow failure.

## 6. Automated test cases

The assertions-based runner in `Code.Tests/Program.cs` contains 20 groups:

| # | Test group | Main expected check |
|---:|---|---|
| 1 | Normal workflow | Cars 002 and 004; correct Standard totals. |
| 2 | Empty dataset | Empty input creates no quotes. |
| 3 | Age 18 boundary | An eligible licensed 18-year-old can rent a non-Luxury car. |
| 4 | Invalid and missing input | Missing rows/IDs, numeric enum values, underage data, and invalid dates are rejected. |
| 5 | Two pricing policies | Standard and discounted amounts are correct. |
| 6 | Stages and input collections stay unchanged | Earlier pipeline stages preserve source collections. |
| 7 | Factory success is used by the validated workflow | A valid Assignment 6 factory outcome proceeds through the pipeline. |
| 8 | Factory rejects missing customer and invalid dates | The factory returns useful reasons for invalid requests. |
| 9 | Pattern matching handles both result cases and eligibility rules | Both factory cases are handled explicitly. |
| 10 | One-day boundary, repeated operation, and state preservation | Repeated successful calls are stable; invalid requests do not mutate input. |
| 11 | Assignment 7 integrated Result workflow succeeds | The result chain returns the expected Standard and Discounted quotes. |
| 12 | Assignment 7 reports distinct domain failures | Invalid ID, missing customer, invalid period, and no available cars return distinct codes. |
| 13 | Option lookup distinguishes present and absent customers | Some/None work, and `false` remains a real license value. |
| 14 | Map transforms a successful value | The function runs and returns the expected transformed value. |
| 15 | Bind continues with the next Result operation | The next operation receives the prior value; its failure is retained. |
| 16 | Map and Bind propagate failure without invoking functions | Both preserve the error and a counter stays at zero. |
| 17 | Valid JSON file loads as one complete dataset | Cars, customers, and rentals load together. |
| 18 | Missing input file becomes an explicit failure | A controlled temporary path returns `DATA_FILE_MISSING`. |
| 19 | Malformed JSON becomes a failure without partial data | Invalid JSON returns `DATA_FILE_MALFORMED`; the temporary file is cleaned up. |
| 20 | Result cases reject a missing payload | Success cannot hold a null payload and Failure cannot hold a null error. |

The file-failure tests use unique temporary paths and clean up after themselves. They do not depend on a file being missing or malformed on the user's computer.

## 7. Requirement-to-file map

| Requirement | File / methods |
|---|---|
| Result cases, Map, Bind, Option | `Result.cs` |
| Domain lookup, validation composition, quote workflow | `RentalWorkflow.cs` |
| JSON loading and boundary exception conversion | `RentalDataLoader.cs` |
| Existing selection, pricing, summary pipeline | `Pipeline.cs` |
| Assignment 6 validation factory and validated request | `RentalRequest.cs` |
| Console workflow and final pattern matching | `Program.cs` |
| Automated assertions | `Code.Tests/Program.cs` |
| Normal and malformed JSON samples | `Data/rental-data.json`, `Data/malformed.json` |

## 8. Contribution

**Azamat Kuanyshpayev - IT-2514 (individual work).** For Assignment 7, I added the generic `Result` and `Option` models, composed the car-rental workflow with `Map` and `Bind`, converted Assignment 6 factory results into explicit workflow outcomes, added JSON-boundary error handling, supplied missing/malformed-file demonstrations, expanded the assertion-based tests, and documented the outcomes.



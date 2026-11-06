# Assignment 6 — Handling Temporal Coupling

## Project information

- **Project:** Console-based Car Rental System
- **Student:** Azamat Kuanyshpayev
- **Group:** IT-2514
- **Work format:** Individual work
- **Target framework:** .NET 10 (`net10.0`)

## 1. Project overview

The application reads cars, customers, and existing rentals from text files. A user enters a customer ID and a rental date range. The application validates the request, finds eligible and available cars, calculates prices using Standard and 10% Discount policies, displays the quotes, and saves the results to `rental-quotes.json`.

Assignment 6 refactors the request workflow so that the application does not rely on a caller remembering an unsafe sequence of operations. The active workflow creates a validated request first and proceeds only when the factory returns the `Valid` case.

## 2. Requirements

Install the **.NET 10 SDK**. The projects target `net10.0`. No third-party test framework is required; the test project uses a small assertions-based test runner included in the repository.

## 3. Build, run, and test

Extract the ZIP archive, open a terminal, and change to the project root—the folder containing `Code.sln` and `Code.csproj`. Run:

```bash
dotnet --version
dotnet restore Code.sln
dotnet build Code.sln
dotnet run --project Code.Tests/Code.Tests.csproj
dotnet run --project Code.csproj
```

The test command prints each test group as `PASS` or `FAIL` and finishes with a summary. Its process exit code is non-zero if any test group fails. Run the application command separately after the tests; the application is interactive and asks for a customer ID, start date, and end date.

The date format is `dd-MM-yyyy`. The application writes `rental-quotes.json` to the current working directory. The `Data` text files are copied to the application output directory during the build.

## 4. Input files and domain rules

| File | Format |
|---|---|
| `Data/cars.txt` | `CarId|Name|Type|Color|Transmission` |
| `Data/customers.txt` | `CustomerId|Name|Age|HasDrivingLicense|PreferredTransmission` |
| `Data/rentals.txt` | `RentalId|CustomerId|CarId|StartDate|EndDate` |

The application applies these rules:

- IDs must be positive, and the requested customer must exist.
- A customer must be at least 18 years old and have a driving licence. A customer must be at least 21 to rent a Luxury car.
- Start and end dates must be present and use `dd-MM-yyyy`; the end date must be later than the start date.
- A car must match the customer's preferred transmission and must not have an overlapping existing rental.
- Daily base prices are Default = 15,000 KZT, Comfort = 20,000 KZT, and Luxury = 30,000 KZT. Automat transmission adds 5,000 KZT per day. The discounted policy applies 10% off the full daily price.
- The rental duration is `EndDate - StartDate`; the return date is not counted as a rental day.

## 5. Assignment 6 implementation

### Task 1 — Identify the hidden dependency

Before this refactor, a caller could manually invoke the pipeline stages with a raw customer and rental period:

```csharp
// Unsafe example for documentation only; it is not the active application workflow.
var period = new RentalPeriod(startDate, endDate);
var available = Pipeline.SelectAvailableCars(cars, rentals, customer, period);
var quotes = Pipeline.CalculateQuotes(available, period, PricingPolicies.Standard);
var result = Pipeline.CreateSummary(customer.Id, period, "Standard", quotes);
```

The caller had to remember to validate the request and execute the stages in the correct order. `RentalPeriod` itself does not enforce a valid date range. For example, `CalculateQuotes` rejects a period with zero or negative days, while skipping the availability-selection stage can allow an incorrectly prepared set of cars to be summarized.

The unsafe example is documentation only. The active `Program.cs` workflow calls `RentalRequestFactory.Create` first and uses `Pipeline.RunValidated` only for a `Valid` result.

### Task 2 — Create validated values with a factory

`RentalRequestFactory.Create` in `RentalRequest.cs` checks the customer ID, customer existence, age and driving-licence eligibility, date format, and date ordering. It returns either:

- `RentalRequestCreationResult.Valid`, which carries a `ValidatedRentalRequest`; or
- `RentalRequestCreationResult.Invalid`, which carries one or more explanatory rejection reasons.

`ValidatedRentalRequest` has read-only properties and an `internal` constructor. Code in another assembly cannot construct it directly using a public constructor; callers obtain the validated value through the factory. The application uses this factory result in its actual pricing workflow.

### Task 3 — Model distinct cases

`RentalRequestCreationResult` is a closed record hierarchy with two mutually exclusive cases:

- **`Valid(ValidatedRentalRequest Request)`** — the request passed factory validation and includes the customer and validated rental period.
- **`Invalid(IReadOnlyList<string> Reasons)`** — validation failed, so no validated request is returned; the reasons explain the failure.

The base result type has a private constructor, restricting the hierarchy to its declared nested cases.

### Task 4 — Handle cases with pattern matching

`Program.cs` uses a `switch` to handle `Valid` and `Invalid` explicitly. For `Valid`, it calculates Standard and Discounted quotes through `Pipeline.RunValidated`. For `Invalid`, it prints each rejection reason and returns without calculating quotes. An unexpected result reaches a rejecting default branch rather than being treated as successful.

### Task 5 — Make operations safe

`Pipeline.RunValidated` requires a `ValidatedRentalRequest` instead of accepting a raw customer and an unchecked rental period as separate inputs. This makes the prerequisite visible in the method signature. If the factory rejects the request, the application does not enter the pricing workflow.

The pipeline produces result values without modifying the source car, customer, or rental collections. Repeating a valid request is allowed: the same request and inputs produce the same quote results. Invalid requests are rejected without committing changes to those collections.

For example, the following kind of call does **not** compile because the API expects a `ValidatedRentalRequest`, not a raw `Customer`:

```csharp
// Intentional compile-time illustration; do not add this as executable code.
Pipeline.RunValidated(cars, rentals, customer, "Standard", PricingPolicies.Standard);
```

## 6. Test cases

Run the assertion-based test runner with:

```bash
dotnet run --project Code.Tests/Code.Tests.csproj
```

The runner contains 10 test groups. Existing Assignment 5 coverage is retained, and the Assignment 6 cases exercise the new factory and validated workflow.

| # | Test group | Expected result |
|---:|---|---|
| 1 | Normal workflow | Cars 002 and 004 are available for the sample two-day rental; each Standard quote is 50,000 KZT. |
| 2 | Empty dataset | Empty input is accepted and produces no quotes. |
| 3 | Age 18 boundary | A licensed 18-year-old can rent a non-Luxury car. |
| 4 | Invalid and missing input | Missing rows/IDs, numeric enum values, underage customer data, and an invalid date range are rejected by parsing/validation. |
| 5 | Two pricing policies | A sample two-day Comfort Automat rental totals 50,000 KZT under Standard and 45,000 KZT under the 10% Discount policy. |
| 6 | Stages and input collections stay unchanged | The three pipeline stages return the expected data, and the original car and rental arrays remain unchanged. |
| 7 | Factory success is used by the validated workflow | A valid factory result is passed to `RunValidated` and creates the expected quotes. |
| 8 | Factory rejects missing customer and invalid dates | An unknown customer, non-positive customer ID, malformed dates, equal dates, and an underage customer are rejected with reasons. |
| 9 | Pattern matching handles both result cases and eligibility rules | The `Valid` case carries a request; the `Invalid` case reports that a customer without a driving licence is rejected. |
| 10 | One-day boundary, repeated operation, and state preservation | A one-day Comfort Automat quote is 25,000 KZT; repeating the same request returns equal quotes; failed validation leaves the source collections unchanged. |

**Verification status:** The solution builds successfully, and all 10 automated test groups pass. Verification was performed using the .NET 10 SDK.

- Build: `dotnet build Code.sln` — successful.
- Tests: `dotnet run --project Code.Tests/Code.Tests.csproj` — 10 passed, 0 failed.

## 7. Sample demonstrations

### Successful request

Use these values when the program prompts you:

- Customer ID: `2`
- Start date: `20-10-2026`
- End date: `22-10-2026`

Customer 2 is licensed and eligible to rent Comfort Automat cars. With the sample data, cars 002 and 004 should be available. Each quote should total **50,000 KZT** under Standard and **45,000 KZT** under the 10% Discount policy. The program should save both results to `rental-quotes.json`.

### Invalid customer — missing prerequisite

Enter customer ID `999` and otherwise correctly formatted dates. The factory should return `Invalid` because the customer does not exist. The program displays the reason and does not calculate quotes.

### Invalid date sequence

Enter customer ID `2` and use the same date for the start and end date, for example `20-10-2026` and `20-10-2026`. The factory should reject the request because the end date must be later than the start date.

### Repeated operation

The automated test `One-day boundary, repeated operation, and state preservation` runs `Pipeline.RunValidated` twice with the same validated request and asserts that the quotes match and the original collections remain unchanged.

## 8. Requirement-to-code map

| Assignment requirement | Implementation |
|---|---|
| Unsafe before example and explanation of hidden order | This README, section 5 — Task 1 |
| Factory validation and access restrictions | `RentalRequest.cs` — `RentalRequestFactory.Create`, `ValidatedRentalRequest` |
| Distinct valid and invalid cases | `RentalRequest.cs` — `RentalRequestCreationResult.Valid` and `.Invalid` |
| Pattern matching in the active workflow | `Program.cs` — switch over the factory result |
| Safe workflow using explicit prerequisites | `Pipeline.cs` — `Pipeline.RunValidated`; called by `Program.cs` |
| Domain pricing and selection logic | `Pipeline.cs` — `SelectAvailableCars`, `CalculateQuotes`, `CreateSummary`, `PricingPolicies` |
| Assertions-based automated checks | `Code.Tests/Program.cs` |
| Input parsing and parsing validation | `TryParse.cs` |
| Sample input data | `Data/cars.txt`, `Data/customers.txt`, `Data/rentals.txt` |

## 9. Contribution

**Azamat Kuanyshpayev — IT-2514 (individual work).** For Assignment 6, I implemented the validated rental-request factory and its `Valid`/`Invalid` result model, integrated the validated path into the active console application, retained earlier pipeline tests, added Assignment 6 assertion-based test coverage, and documented the unsafe and safe workflows.

## 10. Submission contents

The ZIP archive includes the C# source files, solution and project files, `Code.Tests`, this README, and the sample files under `Data/`. Generated `bin` and `obj` directories are excluded. Build and test the project after extracting the archive, using the commands in Section 3.

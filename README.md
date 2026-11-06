Topic: Console-based Car Rental System.
Intended users: Rental staff.
Team: Individual work.
Purpose: Find cars a customer can rent for selected dates, calculate quotes, compare two price policies, and save the results to JSON.


Input data
CarId|Name|Type|Color|Transmission
CustomerId|Name|Age|HasDrivingLicense|PreferredTransmission
RentalId|CustomerId|CarId|StartDate|EndDate
Dates must use dd-MM-yyyy.

IDs must be positive numbers. Car types must be Default, Comfort, or Luxur; transmission must be Manual or Automat. Empty rows, missing fields, invalid dates, customers under 18, and invalid license values are rejected with a message.

Business rules

1. A customer must be at least 18 and have a driving license.
2. A customer must be at least 21 to rent a Luxury car.
3. The selected transmission must match the car's transmission.
4. A car is unavailable if an existing rental overlaps the requested dates.
5. Daily category prices are Default = 15,000 KZT, Comfort = 20,000 KZT, Luxury = 30,000 KZT.
6. Automat transmission adds 5,000 KZT per day.
7. The Standard policy uses the full daily price. The Discounted policy gives 10% off the full daily price. The selected policy is passed to the same pipeline as a function.
8. Rental duration is the number of days between the start and end dates. The end date is the return date and is not counted as a rental day.

Three pipeline stages

| Stage | Method | Input -> output | Example |

| 1. Select cars | Pipeline.SelectAvailableCars | Cars and rentals -> available cars | For a 20-year-old who selected Automat, exclude Luxury cars and cars already rented. |
| 2. Calculate quotes | Pipeline.CalculateQuotes | Available cars and a Func<Car, decimal> price policy -> quotes | A Comfort Automat car costs 25,000 KZT per day under Standard. |
| 3. Create result | Pipeline.CreateSummary | Quotes -> sorted RentalSearchResult | Quotes are ordered by total price. |

Pipeline.Run calls the three stages in order. CalculateQuotes is the higher-order function: it receives and invokes the supplied price policy. These pipeline methods do not read the console, write files, or change their input collections. Input parsing is in TryParse.cs; console and JSON operations happen in Program.cs after calculation.

Expected sample result

The sample files contain five cars. The customer with ID 002 is 20 years old and selected Automat. For a request from 14-10-2026 to 16-10-2026:

Cars 002 and 004 should be returned as available.
Car  003 should be excluded because its rental overlaps the requested dates.
Car  005 should be excluded because a 20-year-old cannot rent a Luxury car.
Car  001 should be excluded because its transmission does not match.
Standard should quote each matching car at 50,000 KZT for two days.
Discounted should quote each matching car at 45,000 KZT for two days.

The same records are processed by both policies, so the only price difference is caused by the selected policy. Customer 001 is exactly 18 and demonstrates the age boundary for a non-Luxury car.


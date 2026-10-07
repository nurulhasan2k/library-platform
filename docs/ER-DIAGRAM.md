# Entity-relationship diagram (draft for Meeting 1)

The proposed data model for the platform. Items, borrowers, loans and staff users come from Assignment 2;
the other tables are new. The three item types (Book, Music, Toy) are stored in **one table** (EF Core "table per hierarchy"),
with the type-specific columns empty for the other types.

![ER diagram](images/er-diagram.png)

Key points to confirm at the meeting:

- `Item.BranchId` is where the copy physically is **now**. A return at another branch moves it; a transfer moves it deliberately.
- One `LibraryCode` is one physical copy (decision 4: holds are per item, not per title).
- "In transit" is not stored on the item: it is derived from an active `Transfer` (Requested or InTransit).
- `Loan.FinePaid` / `FinePaidOn` let reception mark a fine collected, so reports can show charged, collected and outstanding.
- `USER_BRANCH` says which branches a Reception user may choose after login.
- `APP_SETTING` holds the admin "simulated today" value (decision 10).
- Owners: Branch, OpeningHours, ReceptionDesk, Category, Item, Borrower, Loan, User, UserBranch, Transfer, AppSetting: **A**.
  Reservation, Notification: **B**. ImportBatch: **C**.

## Source (renders on GitHub)

```mermaid
erDiagram
    BRANCH ||--o{ OPENING_HOURS : "has"
    BRANCH ||--o{ RECEPTION_DESK : "has"
    BRANCH ||--o{ ITEM : "holds (physical location)"
    BRANCH ||--o{ USER_BRANCH : "staff may work at"
    USER ||--o{ USER_BRANCH : "assigned to"
    CATEGORY ||--o{ ITEM : "classifies"
    ITEM ||--o{ LOAN : "is lent in"
    BORROWER ||--o{ LOAN : "borrows"
    RECEPTION_DESK |o--o{ LOAN : "processed at"
    ITEM ||--o{ TRANSFER : "moves via"
    BRANCH ||--o{ TRANSFER : "from / to"
    ITEM ||--o{ RESERVATION : "is held by"
    BORROWER ||--o{ RESERVATION : "places"
    BORROWER ||--o{ NOTIFICATION : "receives"
    LOAN |o--o{ NOTIFICATION : "about"
    USER ||--o{ IMPORT_BATCH : "runs"

    BRANCH {
        int Id PK
        string Code UK
        string Name
        string Address
        string Phone
        bool IsActive
    }
    OPENING_HOURS {
        int Id PK
        int BranchId FK
        int DayOfWeek
        time OpenTime "null = closed"
        time CloseTime "null = closed"
    }
    RECEPTION_DESK {
        int Id PK
        int BranchId FK
        string Name
    }
    CATEGORY {
        int Id PK
        string Name
        string ItemType "Book / Music / Toy"
    }
    ITEM {
        int Id PK
        string LibraryCode UK "one code = one physical copy"
        string Name
        string Description
        string ItemType "Book / Music / Toy"
        string Status "Available / Borrowed / Damaged / Destroy"
        int BranchId FK "where the copy is now"
        int CategoryId FK
        string Author "Book"
        string Genre "Book"
        string Artist "Music"
        int Year "Music"
        string ToyType "Toy"
        int RecommendedAge "Toy"
    }
    BORROWER {
        int Id PK
        string CardNumber UK
        string PinHash
        string FullName
        string Email
        string Phone
        string PreferredChannel "Email / Sms / Both"
    }
    LOAN {
        int Id PK
        int ItemId FK
        int BorrowerId FK
        date BorrowedOn
        date DueOn
        date ReturnedOn "null = active"
        decimal FineAmount
        bool FinePaid
        date FinePaidOn
        int BorrowedAtBranchId FK
        int ReturnedAtBranchId FK
        int DeskId FK
        bool ReturnedDamaged
    }
    USER {
        int Id PK
        string Username UK
        string PasswordHash
        string Role "Admin / Reception / Manager"
        string FullName
    }
    USER_BRANCH {
        int UserId FK
        int BranchId FK
    }
    TRANSFER {
        int Id PK
        int ItemId FK
        int FromBranchId FK
        int ToBranchId FK
        string Status "Requested / InTransit / Received / Cancelled"
        datetime RequestedAt
        int RequestedByUserId FK
        datetime DispatchedAt
        datetime ReceivedAt
        string Note
    }
    RESERVATION {
        int Id PK
        int ItemId FK
        int BorrowerId FK
        string Status "Waiting / Ready / Fulfilled / Cancelled / Expired"
        datetime CreatedAt
        datetime NotifiedAt
        datetime ExpiresAt
        string PlacedVia "Kiosk / Web / Desk"
    }
    NOTIFICATION {
        int Id PK
        string Type
        string Channel "Email / Sms"
        int BorrowerId FK
        int ItemId FK
        int LoanId FK
        string Recipient
        string Subject
        string Body
        datetime CreatedAt
        string Status "Simulated sent"
        string DedupeKey UK
    }
    IMPORT_BATCH {
        int Id PK
        string Source "Csv / MockProvider"
        string FileName
        int ByUserId FK
        datetime At
        int RowsTotal
        int RowsCreated
        int RowsRejected
        string ErrorSummary
    }
    APP_SETTING {
        string Key PK
        string Value "for example SimulatedToday"
    }
```

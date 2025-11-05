# Bakery POS - Backend API

This repository contains the backend API for the Bakery Point of Sale system. It is built with .NET 9 and is responsible for managing users, products, inventory, sales, and generating automated reports.

---

## Technology Stack

*   **Framework:** ASP.NET Core 9 Web API
*   **Language:** C#
*   **Database:** SQL Server
*   **ORM:** Entity Framework Core 9 (Code-First approach)
*   **Authentication:** JWT (JSON Web Tokens)
*   **Real-time:** SignalR (for future real-time features)
*   **Notifications:** Telegram Bot API
*   **Mapping:** AutoMapper

---

## 🚀 Getting Started

### Prerequisites

*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [SQL Server Express LocalDB](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (usually installed with Visual Studio)
*   An IDE like [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

### Installation and Setup

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/your-username/bakery-pos-system.git
    cd bakery-pos-system
    ```

2.  **Configure your secrets:**
    *   Navigate to the `BakeryPOS.API` project folder.
    *   Rename `appsettings.Development.json.template` to `appsettings.Development.json`.
    *   Open `appsettings.Development.json` and fill in your secrets:
        *   `AppSettings:TokenKey`: A long, random string for JWT signing.
        *   `TelegramSettings:BotToken`: Your Telegram Bot API token.
        *   `TelegramSettings:ChatId`: Your personal Telegram Chat ID for receiving reports.

3.  **Apply database migrations:**
    *   Open the project in Visual Studio.
    *   Open the **Package Manager Console**.
    *   Run the following command to create the database and apply all migrations:
    ```powershell
    Update-Database
    ```
    This will also seed the initial `admin` user (Username: `admin`, Password: `password`).

4.  **Run the application:**
    *   Press `F5` or the green play button in Visual Studio to launch the API.
    *   The Swagger UI will open in your browser, where you can explore and test all the API endpoints.

---

## API Features

The API provides a comprehensive set of endpoints for managing the POS system.

### Authentication (`/api/auth`)
*   `POST /login`: Authenticate a user and receive a JWT.

### Admin (`/api/admin`)
*   `POST /users`: (Admin only) Create new users (e.g., cashiers).
*   `POST /customers/{id}/adjust-balance`: (Admin only) Manually adjust a customer's balance.

### Products (`/api/products`)
*   Full CRUD (Create, Read, Update, Delete) functionality for managing products.
*   `GET` endpoints are public, while `POST`, `PUT`, `DELETE` are admin-protected.

### Sales (`/api/sales`)
*   `POST /`: (Authenticated) Process a new sale, automatically updating product inventory. Supports cash, card, and credit sales.

### Customers (`/api/customers`)
*   Full CRUD for managing premium customers.
*   `POST /{id}/payments`: Record payments from customers to settle their credit balance.

### Inventory (`/api/inventory`)
*   `POST /add`: Add new stock for a product.
*   `GET /history/{id}`: View the complete stock movement history for a product.

### Reports (`/api/reports`)
*   `GET /`: Get a list of all historically generated reports.
*   `GET /{id}`: Get the full data for a specific report.

---

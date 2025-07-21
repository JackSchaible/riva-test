# Contact Manager

A full-stack contact management application built with Vue.js and ASP.NET Core, featuring a modern Material Design interface for managing personal contacts.

## Features

- **Contact Management**: Create, read, update, and delete contacts
- **Search Functionality**: Search contacts by name or email with real-time results
- **Responsive Design**: Material Design interface that works on desktop and mobile
- **Click-to-Contact**: Email and phone links that open appropriate applications
- **Form Validation**: Client and server-side validation for data integrity
- **Modern UI**: Dark-themed contact cards with intuitive icon-based actions
- **CORS Configuration**: Properly configured Cross-Origin Resource Sharing for secure client-server communication
- **Cloud Deployment**: Automated CI/CD pipeline with frontend deployed to AWS Amplify and backend to AWS ECS/Fargate

## Prerequisites

Before running this application, ensure you have the following installed:

- **Node.js** (v18 or later) - [Download here](https://nodejs.org/)
- **pnpm** (recommended) or npm for package management
- **.NET CLI** (v9.0 or later) - [Download here](https://dotnet.microsoft.com/download)
- **SQL Server** or **SQL Server Express** (for database)

## Project Structure

```text
riva-test/
├── client/                 # Vue.js frontend application
│   ├── src/
│   │   ├── components/     # Vue components
│   │   ├── models/         # TypeScript interfaces
│   │   ├── services/       # API service layer
│   │   └── ...
│   └── package.json
├── server/                 # ASP.NET Core backend
│   ├── ContactManager/     # Main API project
│   │   ├── Controllers/    # API controllers
│   │   ├── Models/         # Data models
│   │   ├── Services/       # Business logic
│   │   └── ...
│   └── ContactManager.Tests/  # Unit tests
└── README.md
```

## Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd riva-test
```

### 2. Frontend Setup (Vue.js)

Start with the client setup since the backend needs to know the client URL for CORS configuration.

Navigate to the client directory:

```bash
cd client
```

Install dependencies:

```bash
pnpm install
# or
npm install
```

Update the API URL in `src/config.ts` if needed:

```typescript
export const config = {
  apiUrl: "https://localhost:7154/api/contacts",
};
```

Start the development server:

```bash
pnpm dev
# or
npm run dev
```

The frontend will be available at `http://localhost:5173`. **Note the port number** - you'll need it for the backend CORS configuration.

### 3. Backend Setup (ASP.NET Core)

Navigate to the server directory and restore dependencies:

```bash
cd server
dotnet restore
```

Configure the application settings. Copy this configuration to `ContactManager/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedOrigins": ["http://localhost:5173", "http://localhost:5174"],
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ContactManager;Trusted_Connection=true;MultipleActiveResultSets=true;"
  }
}
```

**Important**: Make sure the `AllowedOrigins` includes your frontend URL. The default configuration includes `http://localhost:5173` and `http://localhost:5174` (common Vite dev server ports). If your frontend runs on a different port, update this array accordingly.

Create the database by running the setup script:

```bash
# Using SQL Server Management Studio, Azure Data Studio, or your favourite SQL editor: Open and execute server/setup.sql
# OR using sqlcmd:
sqlcmd -S "(localdb)\mssqllocaldb" -i setup.sql
```

Run the application:

```bash
cd ContactManager
dotnet run
```

The API will be available at `https://localhost:7154` (or the port shown in the console).

### 4. Database Setup

The application uses Dapper with SQL Server. Run the provided SQL setup script to create the database and tables:

#### Option 1: Using SQL Server Management Studio (SSMS)

1. Open SQL Server Management Studio
2. Connect to your SQL Server instance
3. Open the file `server/setup.sql`
4. Execute the script

#### Option 2: Using sqlcmd command line

```bash
cd server
sqlcmd -S "(localdb)\mssqllocaldb" -i setup.sql
```

#### Option 3: Using Azure Data Studio or similar tools

1. Open your preferred SQL client
2. Connect to your SQL Server instance
3. Open and execute `server/setup.sql`

The script will:

- Create the `ContactManager` database if it doesn't exist
- Create the `Contacts` table with proper schema
- Add performance indexes for searching
- Insert sample data (optional - you can remove this section from the script)

## Usage

### Adding Contacts

1. Click the "Add Contact" button
2. Fill in the required fields (First Name, Last Name, Email, Phone)
3. Click "Save" to create the contact

### Searching Contacts

The search functionality includes the following features:

- **Minimum Characters**: Search requires at least 3 characters
- **Debounced Input**: Search executes 500ms after you stop typing
- **Multi-term Search**: The search breaks your query on spaces and searches each term against:
  - First Name
  - Last Name
  - Email Address
- **Real-time Results**: Results update as you type (after the 3-character minimum)

**Example**: Searching for "john doe" will find contacts where:

- First name contains "john" AND last name contains "doe", OR
- First name contains "doe" AND last name contains "john", OR
- Email contains both "john" and "doe" in any order

### Contact Actions

- **Edit**: Click the pencil icon to modify contact details
- **Delete**: Click the trash icon to remove a contact (with confirmation)
- **Email**: Click any email address to open your default email client
- **Phone**: Click any phone number to initiate a call (on mobile devices)

## Running Tests

### Backend Tests

```bash
cd server
dotnet test
```

### Frontend Tests

```bash
cd client
pnpm test
# or
npm test
```

## Development

### Backend Development

The backend uses:

- **ASP.NET Core 9.0** - Web API framework
- **Entity Framework Core** - ORM for database operations
- **SQL Server** - Database
- **xUnit** - Testing framework

### Frontend Development

The frontend uses:

- **Vue 3** - Progressive JavaScript framework
- **TypeScript** - Type-safe JavaScript
- **Vuetify 3** - Material Design component library
- **Vite** - Build tool and development server
- **Axios** - HTTP client for API calls

## API Endpoints

- `GET /api/contacts` - List all contacts
- `GET /api/contacts/search?query={query}` - Search contacts
- `POST /api/contacts` - Create a new contact
- `PUT /api/contacts/{id}` - Update an existing contact
- `DELETE /api/contacts/{id}` - Delete a contact

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License.

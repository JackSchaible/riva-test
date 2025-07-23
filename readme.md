# Contact Manager

A full-stack contact management application built with Vue.js and ASP.NET Core, featuring a modern Material Design interface for managing personal contacts. Deployed on AWS with a comprehensive CI/CD pipeline utilizing ECS/Fargate, Amplify, and a full suite of cloud services.

## 🚀 Live Demo

**Production Application**: [https://riva.jackschaible.ca](https://riva.jackschaible.ca)

Experience the fully deployed application running on AWS infrastructure with automatic CI/CD.

## Features

- **Contact Management**: Create, read, update, and delete contacts
- **Search Functionality**: Search contacts by name or email with real-time results
- **Responsive Design**: Material Design interface that works on desktop and mobile
- **Click-to-Contact**: Email and phone links that open appropriate applications
- **Form Validation**: Client and server-side validation for data integrity
- **Modern UI**: Dark-themed contact cards with intuitive icon-based actions
- **CORS Configuration**: Properly configured Cross-Origin Resource Sharing for secure client-server communication
- **Cloud Deployment**: Automated CI/CD pipeline with frontend deployed to AWS Amplify and backend to AWS ECS/Fargate
- **Docker Support**: Backend containerized with Docker Compose for easy local development
- **Integration Testing**: Comprehensive test suite with Docker-based test database
- **AWS Infrastructure**: Full-stack deployment on AWS using ECR, ECS, VPC, ALB, Route 53, and more

## Prerequisites

Before running this application, ensure you have the following installed:

- **Node.js** (v18 or later) - [Download here](https://nodejs.org/)
- **pnpm** (recommended) or npm for package management
- **.NET CLI** (v9.0 or later) - [Download here](https://dotnet.microsoft.com/download)
- **Docker** (optional, for containerized development) - [Download here](https://www.docker.com/get-started)
- **SQL Server** or **SQL Server Express** (for manual setup, not needed with Docker)

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
│   ├── ContactManager.Tests/          # Unit tests
│   └── ContactManager.IntegrationTests/  # Integration tests
├── db/                     # Database setup scripts
├── docker-compose.yml      # Production Docker Compose
├── docker-compose.test.yml # Test environment Docker Compose
└── README.md
```

## Setup Instructions

Choose one of the following setup methods:

### Option 1: Docker Compose (Recommended for Backend)

The fastest way to get the backend running with a database:

```bash
# Clone and navigate to project
git clone <repository-url>
cd riva-test

# Start backend + SQL Server with Docker Compose
docker compose -f docker-compose.test.yml up

# In another terminal, start the frontend
cd client
pnpm install && pnpm dev
```

- **Backend API**: Available at `http://localhost:80`
- **Frontend**: Available at `http://localhost:5173`
- **SQL Server**: Runs in container with automatic schema setup

### Option 2: Manual Development Setup

#### 1. Clone the Repository

```bash
git clone <repository-url>
cd riva-test
```

#### 2. Frontend Setup (Vue.js)

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
  apiUrl: "https://localhost:7154/api/contact",
};
```

Start the development server:

```bash
pnpm dev
# or
npm run dev
```

The frontend will be available at `http://localhost:5173`. **Note the port number** - you'll need it for the backend CORS configuration.

#### 3. Backend Setup (ASP.NET Core)

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

#### 4. Database Setup

The application uses Dapper with SQL Server. Run the provided SQL setup script to create the database and tables:

##### Option 1: Using SQL Server Management Studio (SSMS)

1. Open SQL Server Management Studio
2. Connect to your SQL Server instance
3. Open the file `server/setup.sql`
4. Execute the script

##### Option 2: Using sqlcmd command line

```bash
cd server
sqlcmd -S "(localdb)\mssqllocaldb" -i setup.sql
```

##### Option 3: Using Azure Data Studio, SSMS, or similar tools

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
dotnet test  # Runs unit tests

# Run integration tests (requires Docker)
dotnet test ContactManager.IntegrationTests
```

### Frontend Tests

```bash
cd client
pnpm test
# or
npm test
```

## CI/CD & Cloud Architecture

This project features a comprehensive CI/CD pipeline that automatically builds, tests, and deploys to AWS:

### GitHub Actions Pipeline

- **Triggered on**: Push to main branch, pull requests
- **Build & Test**: Runs unit and integration tests with Docker
- **Deploy**: Automatic deployment to AWS ECS/Fargate and Amplify

### AWS Infrastructure

The entire stack runs on AWS with minimal hand-holding:

#### **Amazon ECR**

- Container registry for all .NET API Docker images

#### **Amazon ECS (Fargate)**

- Serverless container hosting for the API with auto-scaling and zero infrastructure management
- awsvpc networking mode in a custom VPC with public & private subnets
- NAT Gateway for outbound traffic and scoped-down Security Groups

#### **Amazon EC2**

- Windows/Linux instances running SQL Server, secured in private subnets

#### **Amazon VPC**

- Custom VPC with:
  - Public subnets (for ALB)
  - Private subnets (for ECS tasks & EC2 database)
  - NAT Gateway for secure egress
  - Security Groups + Network ACLs for micro-segmented access

#### **Application Load Balancer (ALB)**

- HTTPS front door with health checks, path-based routing to ECS
- Sticky sessions support (if needed)

#### **AWS Certificate Manager (ACM)**

- Free TLS certificates for both ALB and CloudFront distribution

#### **Amazon Route 53**

- DNS hosting & alias records for custom domains (API + frontend)

#### **AWS Amplify Console**

- Git-driven CI/CD for the frontend (Vue.js)
- CodeBuild & CodePipeline under the hood
- S3 for build artifacts
- CloudFront CDN with instant cache invalidation
- Custom domain support, environment variables, branch previews

#### **AWS IAM**

- Least-privilege roles/policies for ECS tasks, CI/CD pipelines, and developers

#### **AWS Secrets Manager**

- Secure storage of database connection strings and other sensitive configurations

#### **Amazon CloudWatch**

- **Logs**: API output, container stdout/stderr, EC2 SQL Server logs
- **Metrics**: CPU, memory, request counts, latency
- **Alarms & Dashboards**: Automated alerts if things go sideways

#### **AWS CloudTrail**

- Audit trail of all management-plane actions for compliance

### Deployment Flow

1. **Code Push** → GitHub triggers workflow
2. **Build** → Docker image built and pushed to ECR
3. **Test** → Integration tests run with Docker Compose
4. **Deploy Backend** → ECS service updated with new image
5. **Deploy Frontend** → Amplify builds and deploys Vue.js app
6. **Health Checks** → ALB verifies service health
7. **DNS** → Route 53 routes traffic to new deployment

## Development

### Backend Development

The backend uses:

- **ASP.NET Core 9.0** - Web API framework
- **Dapper** - Micro ORM for database operations
- **SQL Server** - Database
- **xUnit** - Testing framework
- **Docker** - Containerization for deployment and testing
- **GitHub Actions** - CI/CD pipeline

### Frontend Development

The frontend uses:

- **Vue 3** - Progressive JavaScript framework
- **TypeScript** - Type-safe JavaScript
- **Vuetify 3** - Material Design component library
- **Vite** - Build tool and development server
- **Axios** - HTTP client for API calls
- **AWS Amplify** - Deployment and hosting

## API Endpoints

- `GET /api/contact` - List all contacts
- `GET /api/contact/search?query={query}` - Search contacts
- `GET /api/contact/{id}` - Get a specific contact
- `POST /api/contact` - Create a new contact
- `PUT /api/contact/{id}` - Update an existing contact
- `DELETE /api/contact/{id}` - Delete a contact
- `GET /api/contact/ping` - Health check endpoint

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License.

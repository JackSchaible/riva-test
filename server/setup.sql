-- Contact Manager Database Setup Script
-- This script creates the database and required tables for the Contact Manager application

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ContactManager')
BEGIN
    CREATE DATABASE ContactManager;
END
GO

-- Use the ContactManager database
USE ContactManager;
GO

-- Create the Contacts table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Contacts' AND xtype='U')
BEGIN
    CREATE TABLE Contacts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FirstName NVARCHAR(50) NOT NULL,
        LastName NVARCHAR(50) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Phone NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
    );
END
GO

-- Create an index on Email for faster searching
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Contacts_Email' AND object_id = OBJECT_ID('Contacts'))
BEGIN
    CREATE INDEX IX_Contacts_Email ON Contacts (Email);
END
GO

-- Create an index on FirstName and LastName for faster searching
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Contacts_Name' AND object_id = OBJECT_ID('Contacts'))
BEGIN
    CREATE INDEX IX_Contacts_Name ON Contacts (FirstName, LastName);
END
GO

-- Insert sample data (optional - remove if you don't want sample data)
IF NOT EXISTS (SELECT * FROM Contacts)
BEGIN
    INSERT INTO Contacts (FirstName, LastName, Email, Phone) VALUES
    ('John', 'Doe', 'john.doe@example.com', '+1-555-0123'),
    ('Jane', 'Smith', 'jane.smith@example.com', '+1-555-0124'),
    ('Bob', 'Johnson', 'bob.johnson@example.com', '+1-555-0125'),
    ('Alice', 'Williams', 'alice.williams@example.com', '+1-555-0126'),
    ('Charlie', 'Brown', 'charlie.brown@example.com', '+1-555-0127');
END
GO

PRINT 'Contact Manager database setup completed successfully!';

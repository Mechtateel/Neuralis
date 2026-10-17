# Neuralis — AI Medical Assistant

Neuralis is a medical information system project designed to support interaction between patients, doctors, and administrators. The project includes a cross-platform application, a backend API, and a separate AI service.

## Overview

The project is designed around three user roles:
- **Patient** — access to patient-oriented features and symptom analysis.
- **Doctor** — patient-related workflows and laboratory-result management.
- **Administrator** — user and doctor management.

The system includes symptom-analysis functionality and a laboratory-result processing workflow. AI-generated information is intended as decision support and should not replace professional medical diagnosis.

 Architecture

- **Client:** .NET MAUI, XAML
- **Backend:** ASP.NET Core Web API
- **Database:** PostgreSQL with Entity Framework Core
- **Authentication:** JWT-based authentication
- **AI service:** Python, FastAPI
- **Document processing:** PDF text extraction and OCR components

## Project Goals

- Build a role-based medical information system.
- Separate the client, backend API, and AI service.
- Support laboratory document processing.
- Explore integration between .NET services and a Python-based AI component.

Repository

The repository contains the application and supporting backend/AI-service code.

 Status

Personal development project. Features and deployment requirements may vary by component.

 Disclaimer

Neuralis is a software project, not a certified medical device. Its output must not be treated as a medical diagnosis.

 Author

GitHub: [@Mechtateel](https://github.com/Mechtateel)

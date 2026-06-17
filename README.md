# sc-dotnet-portfolio
A collection of focused .NET projects built to sharpen real-world C# skills — from API design patterns to document processing and automated testing.


📁 Projects

📦 ApiRateLimiter

A .NET implementation of API rate-limiting middleware. Demonstrates how to control request throughput to protect services from abuse and ensure fair usage across consumers.

Key concepts covered
Request throttling strategies (fixed window, sliding window)
Middleware pipeline integration in ASP.NET Core
In-memory and configurable rate-limit policies



📄 GoogleDocParser + GoogleDocParser.Tests

A utility library for parsing and extracting structured content from Google Documents, paired with a full test suite.

Key concepts covered:

Google Docs API integration
Document structure traversal (paragraphs, tables, headings)
Clean separation of parsing logic and business concerns
Unit testing with xUnit / NUnit


🧰 Tech Stack

Technology Usage : C#  Primary language (95%)
ASP.NET Core : Web API & middleware
T-SQL : Data layer / stored procedures
.NET SDK : Runtime & tooling
xUnit / NUnit : Automated testing


🚀 Getting Started

Prerequisites

.NET SDK 8.0+
Visual Studio 2022 or VS Code with C# Dev Kit
(For GoogleDocParser) A Google Cloud project with the Docs API enabled and a service account credentials file

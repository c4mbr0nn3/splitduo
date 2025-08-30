# SplitDuo

A lightweight, open-source expense splitting application designed for couples and two-person households. SplitDuo provides a simple alternative to existing solutions like Splitwise and Cospend.

## Features

- **Two-User Focus**: Optimized for couples and partners
- **Mobile-First Design**: Responsive UI optimized for smartphones
- **Expense Tracking**: Add, edit, and manage shared expenses
- **Automatic Split Calculations**: See who owes what instantly
- **Secure Authentication**: Protected login system
- **Data Import/Export**: Import from Cospend, export to CSV
- **Self-Hosted**: Complete control over your data

## Tech Stack

- **Backend**: .NET Web API
- **Frontend**: Vue.js with Nuxt UI
- **Database**: PostgreSQL
- **Deployment**: Docker & Docker Compose

## Quick Start

### Prerequisites

- Docker and Docker Compose
- Git

### Installation

1. Clone the repository:

```bash
git clone https://github.com/yourusername/splitduo.git
cd splitduo
```

2. Start the application:

```bash
docker-compose up -d
```

3. Access the application at `http://localhost:3000`

## Documentation

- [Project Specification](docs/splitduo_project_spec.md)
- [Database Schema](docs/splitduo_database_schema.txt)
- [REST API Structure](docs/rest_api_structure.md)
- [API DTO Definitions](docs/api_dto_definitions.md)

## API Overview

The SplitDuo API provides endpoints for:

- **Authentication**: Login, register, token refresh
- **Users**: Profile management
- **Groups**: Create and manage expense groups
- **Expenses**: Track and split expenses
- **Settlements**: Record payments between users
- **Balances**: View current balances and suggestions
- **Import/Export**: Data migration and backup

Base URL: `https://splitduo.app/api/v1`

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

SplitDuo is licensed under the MIT License.

## Support

For issues and feature requests, please use the GitLab issue tracker.

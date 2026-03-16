# maalca-api Implementation Plan

> **Project:** maalca-api (.NET 8 Backend)
> **Purpose:** Implement API contracts required by maalca-web frontend
> **Based on:** maalca-integration-master-backlog.md

---

## 📋 Implementation Phases

### Phase 1: Foundation (Unblock Frontend)
1. **Authentication Module** - JWT-based auth with refresh tokens
2. **Multi-Tenant Configuration** - Affiliate/branding settings
3. **Database Setup** - EF Core with code-first migrations

### Phase 2: Core Business Modules
4. **Customers (CRM)** - Full CRUD with pagination
5. **Appointments** - Scheduling with conflict detection
6. **Services** - Service catalog management
7. **Inventory** - Stock tracking with movements
8. **Metrics** - KPIs and analytics

### Phase 3: Advanced Features
9. **Virtual Queue** - Real-time with SignalR
10. **Team Management** - Employee CRUD
11. **Products** - Store catalog
12. **Invoicing** - Billing system
13. **Gift Cards** - Digital gift cards
14. **Campaigns** - Marketing campaigns

### Phase 4: Public Endpoints
15. **Leads** - Property and CiriSonic lead capture

---

## 🏗️ Architecture Overview

```mermaid
graph TB
    subgraph "maalca-api"
        API[API Layer<br/>Minimal APIs]
        CTRL[Controllers<br/>(if needed)]
        SW[SignalR Hub]
        
        subgraph "Application Layer"
            AUTH[Auth Service]
            AFF[Affiliate Service]
            CRM[Customer Service]
            APPT[Appointment Service]
            INV[Inventory Service]
            QUEUE[Queue Service]
            TEAM[Team Service]
            PROD[Product Service]
            INVoice[Invoice Service]
            GC[GiftCard Service]
            CAMP[Campaign Service]
            LEAD[Lead Service]
        end
        
        subgraph "Infrastructure"
            DB[(EF Core<br/>SQL Server)]
            JWT[JWT Handler]
            EMAIL[Email Service]
        end
    end
    
    WEB[maalca-web<br/>Next.js] --> API
    WEB --> SW
```

---

## 📦 Project Structure

```
src/
├── Maalca.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Controllers/
│   │   └── (minimal APIs in modules)
│   ├── Hubs/
│   │   └── QueueHub.cs
│   └── Middleware/
│       └── JwtMiddleware.cs
│
├── Maalca.Application/
│   ├── Common/
│   │   ├── Interfaces/
│   │   ├── DTOs/
│   │   └── Behaviors/
│   └── Dependencies.cs
│
├── Maalca.Domain/
│   ├── Common/
│   │   ├── BaseEntity.cs
│   │   └── AuditableEntity.cs
│   ├── Entities/
│   │   └── (shared entities)
│   └── Enums/
│
├── Maalca.Infrastructure/
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Services/
│   │   └── EmailService.cs
│   └── Identity/
│       └── JwtSettings.cs
│
└── Modules/
    ├── Auth/
    ├── Affiliates/
    ├── Customers/
    ├── Appointments/
    ├── Services/
    ├── Inventory/
    ├── Queue/
    ├── Team/
    ├── Products/
    ├── Invoices/
    ├── GiftCards/
    ├── Campaigns/
    └── Leads/
```

---

## 🔐 Phase 1: Authentication (API-REQ-001, 001b)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Authenticate user, return JWT |
| POST | `/api/auth/refresh` | Refresh JWT token |

### Request/Response Models

**POST /api/auth/login**
```json
// Request
{
  "email": "string",
  "password": "string"
}

// Response
{
  "token": "string",
  "refreshToken": "string",
  "user": {
    "id": "guid",
    "email": "string",
    "affiliateId": "string",
    "role": "string"
  }
}
```

**POST /api/auth/refresh**
```json
// Request
{
  "token": "string",
  "refreshToken": "string"
}

// Response
{
  "token": "string",
  "refreshToken": "string"
}
```

### Implementation Tasks
- [ ] Install NuGet: Microsoft.AspNetCore.Authentication.JwtBearer
- [ ] Configure JWT settings in appsettings.json
- [ ] Create JWT generation service
- [ ] Create AuthController with login/refresh endpoints
- [ ] Implement password hashing (bcrypt)
- [ ] Create user entity and repository

---

## 🏢 Phase 1: Multi-Tenant Config (API-REQ-002)

### Endpoint Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}` | Get affiliate configuration |

### Response Model

```json
{
  "id": "string",
  "branding": {
    "logo": "string",
    "primaryColor": "#hex",
    "secondaryColor": "#hex",
    "heroImage": "string"
  },
  "modules": ["string"],
  "features": {
    "enableQueue": true,
    "enableInventory": true
  },
  "settings": {}
}
```

### Implementation Tasks
- [ ] Create Affiliate entity
- [ ] Create Affiliate repository
- [ ] Implement GET endpoint with tenant isolation

---

## 👥 Phase 2: Customers/CRM (API-REQ-003)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/customers` | List with pagination |
| POST | `/api/affiliates/{affiliateId}/customers` | Create customer |
| PUT | `/api/affiliates/{affiliateId}/customers/{id}` | Update customer |
| DELETE | `/api/affiliates/{affiliateId}/customers/{id}` | Delete customer |

### Query Parameters
- `page` (default: 1)
- `limit` (default: 20)
- `search` (optional)
- `status` (optional)

### Response Model (Paginated)
```json
{
  "data": [],
  "total": 0,
  "page": 1,
  "totalPages": 1
}
```

---

## 📅 Phase 2: Appointments (API-REQ-004, 004b)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/appointments` | List appointments |
| POST | `/api/affiliates/{affiliateId}/appointments` | Create appointment |
| PATCH | `/api/affiliates/{affiliateId}/appointments/{id}` | Update status |
| GET | `/api/affiliates/{affiliateId}/services` | List services |
| POST | `/api/affiliates/{affiliateId}/services` | Create service |
| PUT | `/api/affiliates/{affiliateId}/services/{id}` | Update service |
| DELETE | `/api/affiliates/{affiliateId}/services/{id}` | Delete service |

### Features Required
- Conflict detection (double-booking prevention)
- Status workflow: scheduled → confirmed → in-progress → completed/cancelled

---

## 📦 Phase 2: Inventory (API-REQ-005)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/inventory` | List inventory |
| POST | `/api/affiliates/{affiliateId}/inventory/movements` | Register movement |

### Movement Types
- `in` - Stock addition
- `out` - Stock reduction

---

## 🔄 Phase 3: Virtual Queue (API-REQ-006)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/queue` | Get queue state |
| POST | `/api/affiliates/{affiliateId}/queue` | Add to queue |
| PATCH | `/api/affiliates/{affiliateId}/queue/{id}` | Update entry status |

### SignalR Hub
- Hub URL: `/hubs/queue?affiliateId={id}`
- Events: `QueueUpdated`, `PositionChanged`, `Called`

### Status Values
- `waiting` → `in_service` → `completed` | `no_show`

---

## 👨‍💼 Phase 3: Team Management (API-REQ-007)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/team` | List team members |
| POST | `/api/affiliates/{affiliateId}/team` | Add team member |
| PUT | `/api/affiliates/{affiliateId}/team/{id}` | Update member |
| DELETE | `/api/affiliates/{affiliateId}/team/{id}` | Remove member |

---

## 🛒 Phase 3: Products (API-REQ-008)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/products` | List products |
| POST | `/api/affiliates/{affiliateId}/products` | Create product |
| PUT | `/api/affiliates/{affiliateId}/products/{id}` | Update product |
| DELETE | `/api/affiliates/{affiliateId}/products/{id}` | Delete product |

---

## 🧾 Phase 3: Invoicing (API-REQ-009)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/invoices` | List invoices |
| POST | `/api/affiliates/{affiliateId}/invoices` | Create invoice |

---

## 🎁 Phase 3: Gift Cards (API-REQ-010)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/giftcards` | List gift cards |
| POST | `/api/affiliates/{affiliateId}/giftcards` | Create gift card |

### Features
- Generate unique code
- Track balance

---

## 📊 Phase 3: Metrics (API-REQ-011)

### Endpoint Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/metrics` | Get KPIs |

### Response Model
```json
{
  "revenue": 0,
  "appointments": 0,
  "customers": 0,
  "inventoryValue": 0,
  "queueLength": 0
}
```

---

## 📢 Phase 3: Campaigns (API-REQ-012)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/campaigns` | List campaigns |
| POST | `/api/affiliates/{affiliateId}/campaigns` | Create campaign |

---

## 📧 Phase 4: Public Endpoints (Leads)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/metrics/overview` | Homepage stats |
| POST | `/api/leads/properties` | Property lead capture |
| POST | `/api/leads/cirisonic` | CiriSonic lead capture |

---

## ⚙️ Common Infrastructure

### Error Response Format
```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": {}
  }
}
```

### Pagination Standard
```
?page=1&limit=20
```

### Tenant Isolation
- All affiliate endpoints require `{affiliateId}` in path
- Alternative: `X-Tenant-Id` header

---

## 🚀 Recommended Implementation Order

1. **Week 1**: Foundation
   - Project setup + NuGet packages
   - Database context
   - Authentication (JWT)
   - Affiliate config

2. **Week 2**: Core CRUD
   - Customers
   - Appointments + Services
   - Inventory

3. **Week 3**: Core CRUD cont.
   - Team
   - Products
   - Metrics

4. **Week 4**: Advanced
   - Invoicing
   - Gift Cards
   - Campaigns

5. **Week 5**: Real-time + Public
   - SignalR Queue
   - Lead endpoints

---

*Generated: Marzo 2026*

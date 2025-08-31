# ⚙️ EMMS – Equipment Management and Maintenance System

The **Equipment Management and Maintenance System (EMMS)** is a digital solution designed to improve the **tracking, management, and maintenance of health facility equipment**. It ensures that biomedical, IT, and other critical equipment is properly monitored, serviced on time, and kept functional to support uninterrupted health service delivery.

---

## 🎯 Project Goal
To provide an efficient, transparent, and centralized system for **equipment inventory management, maintenance scheduling, fault reporting, and performance monitoring**, ultimately extending equipment lifespan and reducing downtime in health facilities.

---

## ✨ Core Features

### 🔐 User Authentication & Roles
- Secure login with role-based access.  
- Roles may include:  
  - Facility Equipment Officer  
  - Maintenance Technician  
  - Regional Supervisor  
  - System Administrator  
- *(Optional)* Audit trail of user activities.  

---

### 🗂️ Equipment Inventory Management
- Register and catalog all facility equipment.  
- Track key details:  
  - Equipment name & category  
  - Serial number & asset ID  
  - Purchase/installation date  
  - Supplier & warranty information  
  - Location (facility/department)  
- Upload equipment-related documents (manuals, warranties, service contracts).  

---

### 🛠️ Maintenance Management
- Schedule **preventive maintenance** (recurring service reminders).  
- Record **corrective maintenance** (repairs after breakdowns).  
- Log spare parts usage and costs.  
- Notifications for **overdue or upcoming maintenance tasks**.  

---

### 📢 Fault Reporting & Work Orders
- Facility staff can **report equipment faults** via the system.  
- Generate **work orders** for technicians.  
- Assign tasks to maintenance teams with priority levels.  
- Track status: *Pending → In Progress → Completed*.  

---

### 📊 Reporting & Analytics
- Equipment status dashboard (functional, under maintenance, decommissioned).  
- Reports on:  
  - Preventive vs corrective maintenance ratios  
  - Maintenance costs by equipment type/facility  
  - Equipment downtime statistics  
- Export reports to **Excel / PDF**.  

---

### 🔄 Lifecycle & Decommissioning
- Track equipment lifespan from purchase to decommissioning.  
- Record disposal reasons (obsolete, irreparable, replaced).  
- Support for asset replacement planning.  

---

### 🔔 Notifications & Alerts *(Optional)*
- Alerts for upcoming maintenance schedules.  
- Notifications for overdue tasks.  
- Email/SMS reminders for supervisors and technicians.  

---

## 👥 Target Audience
- **Facility-level staff** managing equipment.  
- **Maintenance technicians** performing servicing and repairs.  
- **Regional/National health managers** overseeing equipment performance.  
- **Policymakers** tracking resource allocation and replacement needs.  

---

## 🛠️ Tech Stack
- **Frontend**: RazorView / Bootstrap (web client).  
- **Backend**: ASP.NET Core.  
- **Database**: Microsoft SQL Server.    
- **Deployment**: On-premise at National level + centralized server for aggregation.  


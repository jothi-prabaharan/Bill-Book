# School

**Status: partial.** The School app, its roles, guardian contacts, students, academic setup, exams, marks, admissions, student attendance, fees, and buildings, spaces and assets are built. Work orders, preventive maintenance and AMC contracts are added as each stage is built.

School is sold on its own, like RetailErp, HRMS and Payroll. One customer can hold any of them, with the same branches and users across all of them. A branch is one campus.

## Signing up

Signing up from the School app starts a 14-day School trial and makes you School's **Owner** in your first branch. A customer who already has another app starts School from **Settings › Applications › Start trial**.

## Roles

School comes with these roles. As with every app, a role belongs to School alone.

| Role | What it can do |
|---|---|
| **Owner** | Everything in School. |
| **Principal** | Everything in students, admissions, attendance, fees and maintenance. Can unlock a closed attendance day and close a work order. |
| **Office Admin** | Students, admissions, attendance and contacts. Can see fees, raise them and print them. Can unlock a closed attendance day. |
| **Accountant** | Fees. Can see students, admissions and contacts. |
| **Teacher** | Takes attendance and enters marks. Can see students. Cannot unlock a closed day or see fees. |
| **Maintenance** | Buildings, spaces, assets and preventive plans. Raises and works on work orders, but cannot close them. Can see AMC contracts. |
| **Viewer** | Sees everything and changes nothing. |

## Guardians

A student's parent or guardian is a **contact**, and can be given a link to the [parent portal](#parent-portal). It is the same contact master RetailErp uses, so a guardian can receive fee demands and pay them. Tick **Guardian** on the contact, and use the **Guardians** filter to list only guardians. Guardian contacts are numbered like customers.

The Guardian checkbox and filter appear only in the School app. A student is not a contact; students are recorded under Students.

## Academic setup

**Students › Academic setup** holds four lists:

| List | What it is |
|---|---|
| **School years** | `2026-27`, with start and end dates. Years in a branch never overlap. One year is **current**: making another current takes it off the old one. A **closed** year takes no new sections, enrolments or exams. |
| **Classes** | LKG to XII, set up when the branch is created. **Order** decides promotion. Deactivate the classes you don't teach. |
| **Sections** | A class in one year, such as VI-A in 2026-27. A **capacity** is a hard limit: a full section refuses the next enrolment. A section with students cannot move to another year or class. |
| **Subjects** | Core, language, elective or co-curricular. |

## Students

**Students › Students › New student** records a student admitted directly. The admission number comes from the **ADM** series (ADM-00001, ADM-00002, …), set under **Settings › Number series**, and is never reused.

- A student needs **one or two guardians**, chosen from the guardian contacts, with exactly one marked **primary**. The primary guardian receives the fee demands. Add a guardian first under **Contacts**, with **Guardian** ticked.
- A new student can be **enrolled** in a section in the same save. An existing student is enrolled from their record. A student is enrolled once per school year, in a section of that year, and a roll number is used once per section.
- A student who leaves (alumni, withdrawn or transferred) needs a leaving date.
- The APAAR or Aadhaar number is hidden on the list except its last four characters.

The list shows the current year's class, section and roll number. Choose a section to see its roll.

## Admissions

**Students › Enquiries** records a parent asking about a seat: the child, the class sought, the school year, the parent's name and phone, and where they heard of the school. An enquiry to follow up needs a follow-up date. **Make application** turns an enquiry into an application, filled in from it. An enquiry that goes no further is marked **Lost**.

**Students › Applications** lists every application. An application is numbered from the **APL** series (APL/2627/00001), which restarts each financial year. It holds the child, the guardian (name, mobile, email and relationship) and the documents received. It moves through these stages:

| Stage | How it is reached |
|---|---|
| **Submitted** | When the application is saved. |
| **Documents verified** | Every document recorded is ticked **Verified**. |
| **Assessed** | With the assessment score. A school that does not assess can skip this stage. |
| **Offered** | A seat is offered. |
| **Admitted** | Only by **Admit**, below. |
| **Rejected** or **Withdrawn** | From any stage before Admitted. |

An admitted, rejected or withdrawn application can no longer be changed.

**Admit** is on an offered application, and needs *Admission: approve*. It:

1. makes the guardian a guardian contact. If a guardian with the same mobile number is already on file, that contact is reused, so a parent of two children has one contact;
2. makes the child a student, numbered from the ADM series, with that guardian as primary;
3. enrols the student in a section of the class sought, if you choose one.

Admit can safely be pressed again, for example after a network error: it never makes a second student or a second guardian.

## Attendance

**Students › Attendance register** takes a section's attendance for one day. Choose the section and the day. Every student on the section's roll is shown as a tile, starting as **Present** on a day not yet taken.

- **Tap a tile** to move it to the next mark: present, absent, late, half day, leave, holiday.
- **From the keyboard**, press a mark's key on a tile and focus moves to the next student: **P** present, **A** absent, **L** late, **H** half day, **V** leave, **O** holiday. Arrow keys move between tiles.
- **All present** marks everyone present. The counts above the tiles show how many carry each mark.

**Save** records the day. A day can be taken only within the section's school year, never for a day still to come, and never in a closed year.

**Lock day** closes a saved day, and teachers can lock their own register. A locked day cannot be changed except by someone who can unlock attendance (*Attendance: unlock*, held by the Principal and the Office Admin). They can change a locked day directly, or **Unlock day** so the teacher can correct it.

Student attendance is separate from staff attendance, which is HRMS's.

## Fees

**Fees › Fee setup** has three lists.

- **Fee heads**: what a fee is for (tuition, admission, exam, transport and a caution deposit to start with). A head posts to **Fee Income** unless you choose another income account. A **refundable** head, such as a caution deposit, posts to **Refundable Deposits**, because it is owed back. A SAC can be recorded for the rare taxable head. School education is exempt, so no GST is charged.
- **Structures**: what a class pays in a school year, such as *Day scholar* for Class VI. Each fee has an amount, how often it falls due (one time, monthly, quarterly, termly or annual) and the day it is due. Frequencies count from the month the school year starts: quarterly falls in months 1, 4, 7 and 10 of the year, and termly in months 1, 5 and 9.
- **Concessions**: a percentage or a fixed amount off one fee for one student, for a period, with a reason. A concession applies only once it is approved (*Fee: approve*), and only to demands raised after that.

**Fees › Fee demands** raises a period's demands. Choose the structure and the month, then **Raise demands**. Every active student of that class and year is billed for what falls due that month, to their primary guardian, less any approved concession. Raising the same month again raises nothing twice. A student with no primary guardian is skipped and named.

Raised demands are drafts. **Post** numbers them from the **FDM** series and posts them to the accounts. The guardian's receivable is debited with the net amount, each fee's income account is credited with the full fee, and any concession is debited to **Discount Given**. A posted demand is never changed. It can be voided, with a reason, only while nothing has been received against it.

**Fees › Fee receipts** takes a payment from a guardian: the amount, how it was paid, and the bank or cash account it went into. It settles the guardian's open demands, the oldest first unless you choose. Anything left over is kept as the guardian's advance. The receipt is numbered from the **FRC** series and posted: the bank is debited, and the guardian's receivable is credited with what it settled, with the rest going to their advance. So the guardian's receivable always equals their open demands. Voiding a receipt reopens the demands it settled.

## Buildings, spaces and assets

**Maintenance › Buildings and spaces** records the campus: each **building** with its number of floors, and each **space** in it, such as a classroom, a lab, an office or the playground, with its floor and capacity. Floor 0 is the ground floor, and a basement is a negative floor. Codes are unique in a branch, such as B1 for a building and B1-204 for a room.

**Maintenance › Facility assets** records what is maintained: an AC, a pump, a projector, a bench. Each asset has a tag, a category, the space it is in, its make, model and serial number, and its purchase date, warranty and cost. The list shows whether each warranty is still running.

Nothing here is deleted, because work orders, preventive plans and AMC contracts refer to it. A space is **deactivated**, and a building only once its spaces are. An asset is **disposed**, and a disposed asset stays disposed.

## Work orders

**Maintenance › Work orders** records what needs fixing and where. Each work order has a title, the asset, the space or both, a priority, the day it was reported and when it is due, and a checklist of tasks. It is numbered from the **WRK** series. Preventive plans and AMC visits raise work orders too.

| State | What it means |
|---|---|
| **Open** | Just raised. This is the only state in which it can be edited. |
| **Assigned** | Someone is on it. The assignee is an employee from **People**, and can be changed. |
| **In progress** | The work has started. It can be put **on hold** and resumed. |
| **Completed** | Done, with the completion date and the labour cost. |
| **Closed** | Signed off. Closing needs *Work orders: close*, which the Principal holds and maintenance staff do not. |
| **Cancelled** | Dropped, with a reason. A work order that has had parts issued cannot be cancelled. |

Tick the checklist as the work goes. **Issue part** takes an item from the store while the work order is assigned, in progress or on hold. It lowers stock and costs the part, so the work order shows its parts cost beside the labour. A part short of stock is refused. The parts come from Inventory's items and stores, so a School branch with no items has nothing to issue yet.

## Preventive plans

**Maintenance › Preventive plans** records jobs that come round on a schedule, such as a quarterly AC service or a monthly fire-extinguisher check. Each plan names the asset, the space or both, how often it recurs (every *n* days, weeks, months, quarters, half-years or years), when it starts and ends, how many days early to raise the work order, and who does it by default.

Every hour, each plan's due dates become **occurrences**, and each occurrence raises one work order on the **Work orders** screen. **Generate now** does the same on demand. Pressing it again, or the hourly run finding the same date, raises nothing twice. A plan starting on the 31st falls on the last day of shorter months and returns to the 31st after them.

The **Occurrences** tab lists every due date. One whose work order is not raised yet can be **skipped**, and one whose work order is raised can be **marked done**. Changing a plan's schedule carries on after the dates already generated.

## AMC contracts

**Maintenance › AMC contracts** records annual maintenance contracts: the vendor (a contact marked as a vendor), the vendor's contract number, the start and end dates, the value and how it is billed, how many visits a year it includes, and whether it is **comprehensive** (parts covered) or labour only. The value is billed through a purchase bill, never from here.

A contract starts as a **draft**. List the assets it covers, then **Activate** it. An asset can be under only one active contract at a time, so a renewal is a new contract starting the day after the old one ends. Once active, a contract's vendor, number, dates, value and cover are fixed; its covered assets, reminder and remarks can still change. A contract can be **terminated** early, with a reason, and it shows as **expired** once its end date passes.

**Record visit** logs each visit, scheduled or for a breakdown, on a day within the contract. A visit for a covered asset can **raise a work order** for the vendor to attend, which appears on the **Work orders** screen. Pressing it twice raises one.

**Renewal reminders.** Set how many days before the end date to be reminded, and the email address to write to. Once a day, each active contract inside that window gets one reminder email, sent from the branch's mailbox. A contract with no reminder address gets none.

## Parent portal

Guardians can see their children's school record on the parent portal, without a staff login.

**Giving a guardian access.** Open the guardian's contact in the School app and press **Portal link**. Send the link to the guardian; it is valid for 30 days. A link made in the School app opens the parent portal, and one made in RetailErp opens a customer's statement. On a student's record, a guardian sees the child only when **portal access** is ticked for them, so a second guardian can be kept off the portal.

**What a guardian sees:**

| Page | What is on it |
|---|---|
| **Home** | Each child with their class and section. The fees raised to the guardian, with what is still owed on each, and the payments they have made, with the demands each one settled and anything kept as an advance. |
| **A child** | A month of attendance, day by day, with a count of each mark and the share of school days attended. The marks of every **published** or **locked** exam, subject by subject, with the pass mark. |

Only posted demands and receipts appear. Drafts and voided documents do not. Fees are raised to a student's primary guardian, so only that guardian sees them. Marks appear once an exam is published, never while teachers are still entering them.

## Exams and marks

**Students › Exams and marks** plans an exam with its subjects for each class, with maximum and pass marks. An exam moves through four states:

| State | What it means |
|---|---|
| **Planned** | The exam and its subjects can still be changed. |
| **Open for marks** | Teachers enter marks, by subject and section. A student can be marked absent. |
| **Published** | The marks are final and can be shown to parents. A published exam can be reopened for a correction. |
| **Locked** | Final. Nothing changes. |

Entering marks needs *Students: edit*, which teachers hold. Opening, publishing and locking an exam need *Students: approve*.

## The School app

The School app has the shared settings screens, the employee master (**People**) and **Contacts**. Its **Students**, **Fees** and **Maintenance** sections are built, and more maintenance screens appear as each is built. HRMS-only screens, such as announcements and policies, are not shown in School.

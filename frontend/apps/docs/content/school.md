# School

**Status: partial.** The School app, its roles, guardian contacts, students, academic setup, exams and marks are built. Admissions, attendance, fees and maintenance are added as each stage is built.

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

A student's parent or guardian is a **contact**. It is the same contact master RetailErp uses, so a guardian can receive fee demands and pay them. Tick **Guardian** on the contact, and use the **Guardians** filter to list only guardians. Guardian contacts are numbered like customers.

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

The School app has the shared settings screens, the employee master (**People**) and **Contacts**. Its **Students** section is built. Fees and Maintenance appear as each is built. HRMS-only screens, such as announcements and policies, are not shown in School.

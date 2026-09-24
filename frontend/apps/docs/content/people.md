# Employees

**Status: partial.** The employee master and organisation setup are built. Leave, attendance, payroll and self-service come next.

The employee master is shared by **HRMS**, **Payroll** and **School**. A customer with more than one of them keeps one record per person, entered once. The screens are under **People** in each of those apps.

## Organisation setup

**People › Organisation setup** holds the five lists every employee is placed in:

| List | What it is |
|---|---|
| **Departments** | Where someone works. A department can have a head and sit under a parent department, but never under itself. |
| **Designations** | Their job title. |
| **Grades** | Their level. **Order** ranks the grades, and a move to a higher grade is recorded as a promotion. Each grade has a notice period, which new employees of that grade start with. |
| **Cost centres** | Optional, for splitting costs. |
| **Work locations** | Where they report. The state decides professional tax and LWF, and **Check-in fence** limits mobile check-in to that distance once attendance is built. |

Each branch starts with one of each (General, Staff, Grade 1 and Head office), so you can add employees the day the branch opens. Codes are unique within a branch. An entry that is no longer used is made inactive rather than deleted, because employees and their history still refer to it.

## Adding an employee

**People › Employees › New employee** opens the employee's record. Its tabs are:

- **Personal**: name, date of birth, gender, marital status, phone and email.
- **Job**: department, designation, grade, location, cost centre, manager, joining date, probation, confirmation, notice period, employment type and status. An employee who has left needs an exit date. **Login** links the employee to a user, for self-service.
- **Statutory**: PAN, Aadhaar, UAN, PF and ESI numbers, and whether PF, ESI, professional tax and LWF apply.
- **Addresses & contacts**: at most one current and one permanent address, and emergency contacts.
- **Family & nominees**: family members, and who is nominated for PF, gratuity and insurance. The shares for each kind of nomination must add up to 100 percent.
- **Bank**: accounts, with exactly one marked as the one salary is paid into.
- **Education & past jobs**, and **Documents & assets**: documents on file, and anything issued to the employee.
- **History**: every change, starting with the joining.

The employee code comes from the **EMP** series (EMP-00001, EMP-00002, …), set under **Settings › Number series**. An employee must be at least 14 on the joining date. A manager cannot report, directly or through others, to the employee who reports to them.

## Who sees PAN, Aadhaar and bank numbers

- **On the employee list**, PAN and Aadhaar are always hidden except their last four characters.
- **On an employee's record**, they are shown in full, with the bank account numbers, only to someone who can see payroll (*Payroll: view*) and to the employees themselves. Anyone else sees them hidden. Saving the record keeps the hidden numbers exactly as they were.

## History

Every change of department, designation, grade, location or manager adds a line to the employee's history, dated from **Change takes effect**. So do confirmation and exit. Lines are never edited or removed: the record shows the present, and the history shows how it got there.

## Announcements and policies (HRMS)

**People › Announcements** posts a notice to everyone, or to one department, location or grade, with a publish date and an optional expiry date. Pinned notices come first.

**People › Policies** holds the policy documents employees read, and marks those they must acknowledge. Acknowledging comes with employee self-service.

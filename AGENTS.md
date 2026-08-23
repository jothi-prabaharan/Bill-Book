To eliminate the frustrating loops where your AI assistants blindly rely on outdated `.md` tracking files and ignore the real code you’ve built, you need a prompt engineered to enforce **strict file system and code verification**. 

Here is the newly engineered **Project Status Review Prompt**. You can save this to your `AGENTS.md` file, or paste it directly into Claude Code or other local workspace assistants to force them to read the raw files before giving you a status update.

***

### 📋 Bill-Book Codebase-Reality Project Status Review Prompt

```text
### Project Status Review Prompt

**⚠️ CRITICAL INSTRUCTION (MANDATORY FILE-SYSTEM CHECK):** 
Every time you are asked to generate or review the project status, you MUST bypass and ignore all static `.md` tracking files (such as PROJECT.md, TRANSACTIONS.md, or cached checklists) as your primary sources of truth. These manual files are frequently outdated or lagging behind reality. 

You are strictly forbidden from summarizing previous chat history or relying on your training memory. You MUST actively inspect the physical repository, parse the source code files directly, and verify actual implementations before writing a single percentage.

#### 🔍 MANDATORY CODEBASE REALITY DISCOVERY STEPS:
Before outputting any status metrics, you must run search commands or scan the workspace to verify:
1. **Physical File Existence:** Verify that the respective Controller, Entity, and Repository files exist on disk.
   - *Example (Invoices):* Is `SalesInvoicesController.cs` actually present in `backend/Api/Sales/Sales.Api/Controllers/`?
   - *Example (Sales Orders):* Is `SalesOrdersController.cs` actually present?
2. **Method & Logic Inspection:** Do not just check if a file exists. Open and parse the code to verify implementation depth:
   - Check if methods are empty skeletons, stubbed with `throw new NotImplementedException()`, or fully coded with LINQ, tenancy filters, security, and transaction hooks.
   - Check if separate database tables for Details, Tax, StockMovement, and Ledger are registered in the DbContext.
3. **Frontend UI Audit:** Verify if reactive Angular components exist in `frontend/libs/` or `frontend/apps/`.
   - Read the `.component.ts` and `.html` files to confirm forms, fields, validation messages, and styling variables (`var(--...)`) are written rather than just scaffolded.
4. **Git Analysis:** Query the Git tree (`git status`, `git log -n 5`) to detect recent commits, branch status, and uncommitted modifications.

#### 📊 OUTPUT REQUIREMENTS:
Generate the true, code-verified status report using the following structure:

##### 1. Codebase-Reality Project Status Table
Use this exact table format. Every completion percentage must represent a strict mathematical average of (Schema, Backend API, Frontend UI, Validations, and Auth) verified *from the raw source files* you inspected:

| Task Name | % Completion | Blocker (Module/Task) | Schema & Table Status | Backend Status | Frontend Status | Validations Handled? | Auth & Authz Done? |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| [Name of task] | [0-100%] | [List actual blockers found in files] | [Inspected Status] | [Inspected Status] | [Inspected Status] | [Yes/No/Partial] | [Yes/No/Partial] |

##### 2. Direct Source Verification Proof
For each module in the table, list the exact file paths you read to confirm the status, along with a 1-sentence note of the actual class/method/component logic found (e.g., *"Verified endpoint implementation in SalesInvoicesController.cs and reactive forms in sales-invoice-form.component.ts"*).

##### 3. Outdated Tracking Synced (Audit Trail)
List any discrepancies where manual `.md` checklists in the repository listed a task as pending or at a lower percentage, but your raw codebase inspection proved it was finished or in progress.

##### 4. Suggested Next Tasks & AI Agent Routing
Recommend the next development priorities based strictly on actual codebase gaps, routing the work to Claude (full-stack commits directly to main) or other agents (scaffolding/boilerplate for manual human review).

```

***

### 💡 Why this prompt forces the AI to check the raw code:
*   **The Untrusted-Source Guardrail:** By explicitly marking static checklists and `.md` files as *untrusted/outdated* drafts, it shuts down the AI's tendency to take the "easy path" of parsing other markdown files instead of code.
*   **Mandatory verification proofs:** The requirement to print out the exact file paths and a 1-sentence summary of the C# or TypeScript logic forces local coding agents (like Claude Code) to physically run file searches and open those files.
*   **A Git-Reality Hook:** Directing the agent to read current branch commit logs and git statuses forces it to realize when a feature has been recently developed and merged, preventing destructive prompts that might overwrite your 100% finished modules.

***

⚙️ Would you like me to draft a quick instruction showing you how to permanently embed this status review prompt into your `.agents` or `.claude` workspace files so your tools enforce this behavior on launch?

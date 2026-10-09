<#
.SYNOPSIS
    Automated test suite for API 7: Customer.Api (Port 4505 & Gateway Port 4500).
#>

[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:4505",
    [string]$GatewayUrl = "http://localhost:4500",
    [string]$InternalKey = "dev-only-internal-key-change-me",
    [string]$SigningKey = "dev-only-signing-key-updated-secure-32-bytes-min-value-2026-xyz"
)

$ErrorActionPreference = 'Continue'

function Base64UrlEncode([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-TestJwt {
    param(
        [string]$Sub = "c7eaac59-406c-4a22-9095-fb11d8466000",
        [string]$CustomerId = "00000000-0000-0000-0d00-000000000001",
        [string]$CustomerCode = "DEMO-RTL",
        [string]$OrgId = "00000000-0000-0000-0d01-000000000001",
        [string]$BusinessType = "Retail",
        [int]$AddOns = 4, # Crm = 4
        [string]$PlanTier = "Elite",
        [string[]]$Permissions = @(
            "crm.view", "crm.edit", "crm.delete",
            "support.view", "support.edit", "support.delete",
            "restaurant.view", "restaurant.edit",
            "contacts.view", "contacts.edit"
        )
    )

    $headerJson = '{"alg":"HS256","typ":"JWT"}'
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $payload = [ordered]@{
        sub = $Sub
        customer_id = $CustomerId
        customer_code = $CustomerCode
        org_id = $OrgId
        business_type = $BusinessType
        add_ons = $AddOns.ToString()
        plan_tier = $PlanTier
        permission = $Permissions
        nbf = $now
        exp = $now + 3600
        iss = "bill-book"
        aud = "bill-book"
    } | ConvertTo-Json -Compress

    $keyBytes = [System.Text.Encoding]::UTF8.GetBytes($SigningKey)
    $hB64 = Base64UrlEncode ([System.Text.Encoding]::UTF8.GetBytes($headerJson))
    $pB64 = Base64UrlEncode ([System.Text.Encoding]::UTF8.GetBytes($payload))
    $signInput = [System.Text.Encoding]::UTF8.GetBytes("$hB64.$pB64")

    $hmac = New-Object System.Security.Cryptography.HMACSHA256 -ArgumentList @(,$keyBytes)
    $sig = Base64UrlEncode ($hmac.ComputeHash($signInput))
    return "$hB64.$pB64.$sig"
}

$results = [System.Collections.Generic.List[PSObject]]::new()

function Record-Test([string]$TestId, [string]$Name, [bool]$Passed, [string]$Details) {
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color = if ($Passed) { "Green" } else { "Red" }
    Write-Host "[$status] ${TestId}: $Name" -ForegroundColor $color
    if (-not $Passed) {
        Write-Host "       Error: $Details" -ForegroundColor Yellow
    }
    $results.Add([PSCustomObject]@{
        TestId = $TestId
        Name = $Name
        Status = $status
        Details = $Details
    })
}

function Invoke-Api {
    param(
        [string]$Uri,
        [string]$Method = "Get",
        [string]$Token,
        [string]$OrgId,
        [hashtable]$ExtraHeaders = @{},
        $Body = $null
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    if ($OrgId) { $headers["X-Org-Id"] = $OrgId }
    foreach ($k in $ExtraHeaders.Keys) { $headers[$k] = $ExtraHeaders[$k] }

    $jsonBody = if ($Body -ne $null) {
        if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 -Compress }
    } else { $null }

    try {
        $params = @{
            Uri = $Uri
            Method = $Method
            Headers = $headers
            ContentType = "application/json"
        }
        if ($jsonBody) { $params["Body"] = $jsonBody }

        $res = Invoke-RestMethod @params
        return @{ StatusCode = 200; Data = $res; Success = $true }
    } catch {
        $status = 0
        $errBody = ""
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $errBody = $reader.ReadToEnd()
            } catch {}
        }
        return @{ StatusCode = $status; Error = $_.Exception.Message; Body = $errBody; Success = $false }
    }
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host " RUNNING BATCH 2 TESTS: API 7 - Customer.Api (Port 4505)" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

$defaultToken = New-TestJwt
$retailOrgId = "00000000-0000-0000-0d01-000000000001"
$retailCustId = "00000000-0000-0000-0d00-000000000001"

# -------------------------------------------------------------
# Section 1: Lead Intake & Lifecycle
# -------------------------------------------------------------
Write-Host "--- SECTION 1: LEADS ---" -ForegroundColor Yellow

# T01: Create Lead
$leadPayload = @{
    name = "Sunil Sharma"
    companyName = "Sharma Retail Ltd"
    phone = "+91 9811223344"
    email = "sunil@sharma.example"
    source = "Website"
}
$r01 = Invoke-Api -Uri "$BaseUrl/api/leads" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $leadPayload
$leadId = if ($r01.Success -and $r01.Data.leadId) { $r01.Data.leadId } else { 0 }
$pass01 = ($r01.Success -and $leadId -gt 0 -and $r01.Data.status -eq "New")
Record-Test "T01" "Lead Intake (POST /api/leads)" $pass01 "LeadId: $leadId, Status: $($r01.Data.status), Raw: $($r01.Body)"

# T02: Read Lead
$r02 = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass02 = ($r02.Success -and $r02.Data.name -eq "Sunil Sharma" -and $r02.Data.phone -eq "+919811223344")
Record-Test "T02" "Get Lead by ID (GET /api/leads/$leadId)" $pass02 "Name: $($r02.Data.name), Phone: $($r02.Data.phone)"

# T03: Transition Lead Status
$updateLeadPayload = @{
    name = "Sunil Sharma"
    companyName = "Sharma Retail Ltd"
    phone = "+91 9811223344"
    email = "sunil@sharma.example"
    source = "Website"
    status = "Contacted"
}
$r03 = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId" -Method Put -Token $defaultToken -OrgId $retailOrgId -Body $updateLeadPayload
$r03_check = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass03 = ($r03.StatusCode -in @(200, 204) -and $r03_check.Data.status -eq "Contacted")
Record-Test "T03" "Update Lead Status to Contacted (PUT /api/leads/$leadId)" $pass03 "Status: $($r03_check.Data.status)"

# T04: Lead Validation (Missing Name)
$badLeadPayload = @{
    companyName = "Nameless Corp"
    email = "nameless@example.com"
}
$r04 = Invoke-Api -Uri "$BaseUrl/api/leads" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $badLeadPayload
$pass04 = ($r04.StatusCode -eq 400)
Record-Test "T04" "Lead Validation - Missing Name rejected with 400" $pass04 "StatusCode: $($r04.StatusCode), Body: $($r04.Body)"

# T05: Multi-Tenant Org Isolation for Leads
$otherOrgId = "00000000-0000-0000-0d01-000000000002"
$otherOrgToken = New-TestJwt -OrgId $otherOrgId -CustomerCode "DEMO-PHA"
$r05 = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId" -Method Get -Token $otherOrgToken -OrgId $otherOrgId
$pass05 = ($r05.StatusCode -in @(403, 404))
Record-Test "T05" "Multi-Tenant Isolation - Lead inaccessible to other OrgId" $pass05 "StatusCode: $($r05.StatusCode)"

# T06: Lead Convert Validation
$badConvert = @{ contactId = 1; createContact = $true }
$r06 = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId/convert" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $badConvert
$pass06 = ($r06.StatusCode -eq 400)
Record-Test "T06" "Lead Convert Validation - Contradictory request rejected with 400" $pass06 "StatusCode: $($r06.StatusCode), Body: $($r06.Body)"

# T07: Lead Convert Execution (Link Existing Contact 1)
$convertPayload = @{ contactId = 1; createContact = $false }
$r07 = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId/convert" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $convertPayload
$r07_check = Invoke-Api -Uri "$BaseUrl/api/leads/$leadId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass07 = ($r07.Success -and $r07_check.Data.status -eq "Converted" -and $r07_check.Data.convertedContactId -eq 1)
Record-Test "T07" "Convert Lead to Contact (POST /api/leads/$leadId/convert)" $pass07 "Status: $($r07_check.Data.status), ConvertedContactId: $($r07_check.Data.convertedContactId)"

# -------------------------------------------------------------
# Section 2: Support Tickets
# -------------------------------------------------------------
Write-Host "`n--- SECTION 2: TICKETS ---" -ForegroundColor Yellow

# T08: Ticket Cross-Org Contact Rejection (Contact 99999)
$crossOrgTicket = @{
    contactId = 99999
    subject = "Foreign Contact Ticket"
    priority = "Medium"
    channel = "Email"
}
$r08 = Invoke-Api -Uri "$BaseUrl/api/tickets" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $crossOrgTicket
$pass08 = ($r08.StatusCode -eq 403)
Record-Test "T08" "Ticket Cross-Org Contact Rejection - Non-existent contact rejected with 403" $pass08 "StatusCode: $($r08.StatusCode)"

# T09: Create Valid Support Ticket (Contact 1)
$ticketPayload = @{
    contactId = 1
    subject = "POS Barcode Scanner Hardware Malfunction"
    description = "Scanner stopped detecting barcodes during checkout rush"
    priority = "High"
    channel = "Email"
}
$r09 = Invoke-Api -Uri "$BaseUrl/api/tickets" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $ticketPayload
$ticketId = if ($r09.Success -and $r09.Data.ticketId) { $r09.Data.ticketId } else { 0 }
$pass09 = ($r09.Success -and $ticketId -gt 0 -and $r09.Data.status -eq "Open" -and $r09.Data.slaDueAt -ne $null)
Record-Test "T09" "Support Ticket Intake (POST /api/tickets)" $pass09 "TicketId: $ticketId, SLA: $($r09.Data.slaDueAt), Status: $($r09.Data.status)"

# T10: Get Support Ticket by ID
$r10 = Invoke-Api -Uri "$BaseUrl/api/tickets/$ticketId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass10 = ($r10.Success -and $r10.Data.subject -eq "POS Barcode Scanner Hardware Malfunction")
Record-Test "T10" "Get Ticket by ID (GET /api/tickets/$ticketId)" $pass10 "Subject: $($r10.Data.subject), Priority: $($r10.Data.priority)"

# T11: Ticket Channel Plan Tier Validation (Call requires Pro/Elite)
$standardToken = New-TestJwt -PlanTier "Standard"
$callTicketPayload = @{
    contactId = 1
    subject = "Urgent Call Support"
    priority = "Urgent"
    channel = "Call"
}
$r11 = Invoke-Api -Uri "$BaseUrl/api/tickets" -Method Post -Token $standardToken -OrgId $retailOrgId -Body $callTicketPayload
$pass11 = ($r11.StatusCode -eq 422)
Record-Test "T11" "Ticket Channel Plan Restriction - Call channel on Standard plan rejected with 422" $pass11 "StatusCode: $($r11.StatusCode), Body: $($r11.Body)"

# T12: Ticket Status Transitions (Open -> InProgress -> Resolved -> Closed)
$r12_a = Invoke-Api -Uri "$BaseUrl/api/tickets/$ticketId/status" -Method Put -Token $defaultToken -OrgId $retailOrgId -Body @{ status = "InProgress" }
$r12_b = Invoke-Api -Uri "$BaseUrl/api/tickets/$ticketId/status" -Method Put -Token $defaultToken -OrgId $retailOrgId -Body @{ status = "Resolved" }
$r12_check = Invoke-Api -Uri "$BaseUrl/api/tickets/$ticketId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass12 = ($r12_b.StatusCode -in @(200, 204) -and $r12_check.Data.status -eq "Resolved" -and $r12_check.Data.resolvedAt -ne $null)
Record-Test "T12" "Ticket Status Transition to Resolved with Timestamp (PUT /api/tickets/$ticketId/status)" $pass12 "Status: $($r12_check.Data.status), ResolvedAt: $($r12_check.Data.resolvedAt)"

# T13: Ticket Message Addition and Thread Read
$msgPayload = @{
    body = "Replacement barcode scanner dispatched via overnight courier."
    isInternal = $false
}
$r13_post = Invoke-Api -Uri "$BaseUrl/api/tickets/$ticketId/messages" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $msgPayload
$r13_get = Invoke-Api -Uri "$BaseUrl/api/tickets/$ticketId/messages" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass13 = ($r13_post.Success -and $r13_get.Success -and @($r13_get.Data).Count -ge 1)
Record-Test "T13" "Ticket Thread Messaging (POST & GET /api/tickets/$ticketId/messages)" $pass13 "Messages count: $(@($r13_get.Data).Count)"

# -------------------------------------------------------------
# Section 3: Loyalty Points (Restaurant Vertical)
# -------------------------------------------------------------
Write-Host "`n--- SECTION 3: LOYALTY ---" -ForegroundColor Yellow

$rstOrgId = "00000000-0000-0000-0d01-000000000006"
$rstCustId = "00000000-0000-0000-0d00-000000000006"
$rstToken = New-TestJwt -OrgId $rstOrgId -CustomerId $rstCustId -CustomerCode "DEMO-RST" -BusinessType "Restaurant"

# T14: Business Type Restriction on Loyalty (Retail blocked from Restaurant Loyalty)
$r14 = Invoke-Api -Uri "$BaseUrl/api/loyalty/contacts/6" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass14 = ($r14.StatusCode -eq 403)
Record-Test "T14" "Business Type Restriction - Loyalty forbidden for Retail (403)" $pass14 "StatusCode: $($r14.StatusCode)"

# T15: Initial Loyalty Balance Read (Restaurant Contact 6)
$r15 = Invoke-Api -Uri "$BaseUrl/api/loyalty/contacts/6" -Method Get -Token $rstToken -OrgId $rstOrgId
$pass15 = ($r15.Success -and $r15.Data.pointsBalance -ne $null)
$initialBalance = if ($pass15) { [int]$r15.Data.pointsBalance } else { 0 }
Record-Test "T15" "Loyalty Read for Contact (GET /api/loyalty/contacts/6)" $pass15 "Initial PointsBalance: $initialBalance"

# T16: Internal Loyalty Earn (Earn 250 points on Bill #101)
$earnPayload = @{
    customerId = $rstCustId
    orgId = $rstOrgId
    contactId = 6
    invoiceId = 101
    documentNo = "RST-INV-101"
    points = 250
}
$r16 = Invoke-Api -Uri "$BaseUrl/internal/loyalty/earn" -Method Post -ExtraHeaders @{ "X-Internal-Key" = $InternalKey } -Body $earnPayload
$r16_check = Invoke-Api -Uri "$BaseUrl/api/loyalty/contacts/6" -Method Get -Token $rstToken -OrgId $rstOrgId
$pass16 = ($r16.Success -and $r16.Data.points -eq 250 -and $r16_check.Data.pointsBalance -eq ($initialBalance + 250))
Record-Test "T16" "Internal Loyalty Earn (POST /internal/loyalty/earn)" $pass16 "Earned: $($r16.Data.points), New Balance: $($r16_check.Data.pointsBalance)"

# T17: Internal Loyalty Idempotency Check (Retry should not add duplicate points)
$r17 = Invoke-Api -Uri "$BaseUrl/internal/loyalty/earn" -Method Post -ExtraHeaders @{ "X-Internal-Key" = $InternalKey } -Body $earnPayload
$r17_check = Invoke-Api -Uri "$BaseUrl/api/loyalty/contacts/6" -Method Get -Token $rstToken -OrgId $rstOrgId
$pass17 = ($r17.Success -and $r17.Data.alreadyRecorded -eq $true -and $r17_check.Data.pointsBalance -eq ($initialBalance + 250))
Record-Test "T17" "Loyalty Earn Idempotency - Duplicate invoice earn detected without adding points" $pass17 "AlreadyRecorded: $($r17.Data.alreadyRecorded), Balance: $($r17_check.Data.pointsBalance)"

# T18: Internal Loyalty Reverse (Void Invoice #101)
$reversePayload = @{
    customerId = $rstCustId
    orgId = $rstOrgId
    invoiceId = 101
}
$r18 = Invoke-Api -Uri "$BaseUrl/internal/loyalty/reverse" -Method Post -ExtraHeaders @{ "X-Internal-Key" = $InternalKey } -Body $reversePayload
$r18_check = Invoke-Api -Uri "$BaseUrl/api/loyalty/contacts/6" -Method Get -Token $rstToken -OrgId $rstOrgId
$pass18 = ($r18.Success -and $r18_check.Data.pointsBalance -eq $initialBalance)
Record-Test "T18" "Internal Loyalty Reverse (POST /internal/loyalty/reverse)" $pass18 "Reversed Points: $($r18.Data.points), Restored Balance: $($r18_check.Data.pointsBalance)"

# -------------------------------------------------------------
# Section 4: Campaign Audiences
# -------------------------------------------------------------
Write-Host "`n--- SECTION 4: CAMPAIGN AUDIENCES ---" -ForegroundColor Yellow

# T19: Create Audience
$audPayload = @{
    name = "High Priority Leads Audience"
    rules = @{
        leadSources = @("Website", "Referral")
    }
    isActive = $true
}
$r19 = Invoke-Api -Uri "$BaseUrl/api/audiences" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $audPayload
$audId = if ($r19.Success -and $r19.Data.audienceId) { $r19.Data.audienceId } else { 0 }
$pass19 = ($r19.Success -and $audId -gt 0 -and $r19.Data.name -eq "High Priority Leads Audience")
Record-Test "T19" "Create Campaign Audience (POST /api/audiences)" $pass19 "AudienceId: $audId, Name: $($r19.Data.name)"

# T20: List Audiences
$r20 = Invoke-Api -Uri "$BaseUrl/api/audiences" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass20 = ($r20.Success -and (@($r20.Data).Count -ge 1))
Record-Test "T20" "List Audiences (GET /api/audiences)" $pass20 "Audiences count: $(@($r20.Data).Count)"

# T21: Audience Count / Preview Resolution
$previewPayload = @{
    leadSources = @("Website")
}
$r21 = Invoke-Api -Uri "$BaseUrl/api/audiences/count" -Method Post -Token $defaultToken -OrgId $retailOrgId -Body $previewPayload
$pass21 = ($r21.Success -and $r21.Data.count -ne $null)
Record-Test "T21" "Audience Recipient Count (POST /api/audiences/count)" $pass21 "Count: $($r21.Data.count), Suppressed: $($r21.Data.suppressedCount)"

# T22: Update Audience
$updateAudPayload = @{
    name = "Updated VIP Leads Audience"
    rules = @{
        leadSources = @("Website")
    }
    isActive = $true
}
$r22 = Invoke-Api -Uri "$BaseUrl/api/audiences/$audId" -Method Put -Token $defaultToken -OrgId $retailOrgId -Body $updateAudPayload
$r22_check = Invoke-Api -Uri "$BaseUrl/api/audiences/$audId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass22 = ($r22.Success -and $r22_check.Data.name -eq "Updated VIP Leads Audience")
Record-Test "T22" "Update Audience (PUT /api/audiences/$audId)" $pass22 "Updated Name: $($r22_check.Data.name)"

# T23: Delete Audience
$r23 = Invoke-Api -Uri "$BaseUrl/api/audiences/$audId" -Method Delete -Token $defaultToken -OrgId $retailOrgId
$r23_check = Invoke-Api -Uri "$BaseUrl/api/audiences/$audId" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass23 = ($r23.StatusCode -in @(200, 204) -and $r23_check.StatusCode -eq 404)
Record-Test "T23" "Delete Audience (DELETE /api/audiences/$audId)" $pass23 "StatusCode: $($r23.StatusCode), Subsequent GET: $($r23_check.StatusCode)"

# -------------------------------------------------------------
# Section 5: Gateway Reverse Proxy Routing
# -------------------------------------------------------------
Write-Host "`n--- SECTION 5: GATEWAY REVERSE PROXY ROUTING ---" -ForegroundColor Yellow

# T24: Gateway Forwarding to Customer.Api
$r24_leads = Invoke-Api -Uri "$GatewayUrl/api/leads" -Method Get -Token $defaultToken -OrgId $retailOrgId
$r24_audiences = Invoke-Api -Uri "$GatewayUrl/api/audiences" -Method Get -Token $defaultToken -OrgId $retailOrgId
$pass24 = ($r24_leads.Success -and $r24_audiences.Success)
Record-Test "T24" "Gateway Reverse Proxy Forwarding (/api/leads & /api/audiences on Port 4500)" $pass24 "Leads Success: $($r24_leads.Success), Audiences Success: $($r24_audiences.Success)"

# -------------------------------------------------------------
# Summary
# -------------------------------------------------------------
Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host " TEST RUN SUMMARY" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$total = $results.Count
$passedCount = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failedCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Total Tests : $total" -ForegroundColor White
Write-Host "Passed      : $passedCount" -ForegroundColor Green
Write-Host "Failed      : $failedCount" -ForegroundColor $(if ($failedCount -eq 0) { "Green" } else { "Red" })

if ($failedCount -gt 0) {
    Write-Host "`nFailed Test Details:" -ForegroundColor Red
    $results | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host " - $($_.TestId) $($_.Name): $($_.Details)" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nAll $total tests passed successfully!" -ForegroundColor Green
}

return ($failedCount -eq 0)

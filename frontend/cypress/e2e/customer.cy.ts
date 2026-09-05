describe('Customer Module E2E', () => {
  beforeEach(() => {
    // Authenticate and set tenant context before tests
    cy.login('test@company.in', 'password123');
    cy.visit('/customer');
  });

  it('should list leads and respect tenant boundaries', () => {
    // Verify lead list loads
    cy.get('bb-lead-list').should('be.visible');
    cy.get('bb-data-grid').should('exist');
    
    // Create new lead
    cy.contains('button', 'Add Lead').click();
    cy.get('bb-lead-form').should('be.visible');
    
    cy.get('input[formControlName="name"]').type('Test Lead 1');
    cy.get('input[formControlName="email"]').type('test@lead.com');
    cy.get('select[formControlName="source"]').select('Website');
    cy.contains('button', 'Save').click();

    // Verify it appeared in grid
    cy.contains('Test Lead 1').should('be.visible');
    
    // Switch tenant and verify it DOES NOT appear (tenant isolation)
    cy.switchTenant('another-tenant-id');
    cy.visit('/customer');
    cy.contains('Test Lead 1').should('not.exist');
  });

  it('should list tickets and allow creating a new ticket', () => {
    cy.visit('/customer/tickets');
    cy.get('bb-ticket-list').should('be.visible');
    
    cy.contains('button', 'New Ticket').click();
    cy.get('bb-ticket-form').should('be.visible');
    
    cy.get('input[formControlName="subject"]').type('My Internet is down');
    cy.get('textarea[formControlName="description"]').type('No connection since morning');
    cy.get('select[formControlName="priority"]').select('High');
    
    // Pick contact using master select lookup
    cy.get('bb-master-select').click();
    cy.contains('.dropdown-item', 'Acme Corp').click(); // assuming mock contact exists
    
    cy.contains('button', 'Save').click();
    
    cy.contains('My Internet is down').should('be.visible');
  });
});

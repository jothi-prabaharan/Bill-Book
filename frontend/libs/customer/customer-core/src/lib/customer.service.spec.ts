import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CustomerService } from './customer.service';
import { Lead, LeadSource, LeadStatus, Ticket, TicketPriority, TicketStatus, TicketMessage } from './models';

describe('CustomerService', () => {
  let service: CustomerService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [CustomerService]
    });
    service = TestBed.inject(CustomerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should get leads', async () => {
    const mockLeads: Lead[] = [{ leadId: 1, orgId: 'org1', name: 'John', source: LeadSource.Website, status: LeadStatus.New }];
    const promise = service.getLeads();
    const req = httpMock.expectOne('/api/leads');
    expect(req.request.method).toBe('GET');
    req.flush(mockLeads);
    expect(await promise).toEqual(mockLeads);
  });

  it('should convert lead', async () => {
    const promise = service.convertLead(1, 100);
    const req = httpMock.expectOne('/api/leads/1/convert');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ contactId: 100 });
    req.flush({});
    await promise;
  });

  // More tests can be added for tickets
});

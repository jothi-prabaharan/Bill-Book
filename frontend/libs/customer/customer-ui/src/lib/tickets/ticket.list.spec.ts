import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { TicketList } from './ticket.list';
import { CustomerService } from '@bill-book/customer-core';

describe('TicketList', () => {
  let component: TicketList;
  let fixture: ComponentFixture<TicketList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, TicketList],
      providers: [CustomerService]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketList);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

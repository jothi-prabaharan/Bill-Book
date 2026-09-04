import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { TicketThreadComponent } from './ticket-thread.component';
import { CustomerService } from '@bill-book/customer-core';

describe('TicketThreadComponent', () => {
  let component: TicketThreadComponent;
  let fixture: ComponentFixture<TicketThreadComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, TicketThreadComponent],
      providers: [CustomerService]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketThreadComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

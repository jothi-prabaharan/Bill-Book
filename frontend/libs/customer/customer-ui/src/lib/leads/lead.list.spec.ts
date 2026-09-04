import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { LeadList } from './lead.list';
import { CustomerService } from '@bill-book/customer-core';

describe('LeadList', () => {
  let component: LeadList;
  let fixture: ComponentFixture<LeadList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, LeadList],
      providers: [CustomerService]
    }).compileComponents();

    fixture = TestBed.createComponent(LeadList);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

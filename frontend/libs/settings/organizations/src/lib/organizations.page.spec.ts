import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OrganizationsPage } from './organizations.page';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { AuthService } from '@bill-book/auth';

describe('OrganizationsPage (TK-133)', () => {
  let component: OrganizationsPage;
  let fixture: ComponentFixture<OrganizationsPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrganizationsPage, HttpClientTestingModule],
      providers: [
        {
          provide: AuthService,
          useValue: {
            session: () => ({ user: { tenantId: 'tenant-1' } })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OrganizationsPage);
    component = fixture.componentInstance;
  });

  it('allows Hrms but not Pos for a School branch', () => {
    component.form = { businessType: 'School', addOns: 'None' } as any;
    
    expect(component['isAddOnAllowed']('Hrms')).toBe(true);
    expect(component['isAddOnAllowed']('Pos')).toBe(false);
  });

  it('allows Pos for a General branch', () => {
    component.form = { businessType: 'Retail', addOns: 'None' } as any;
    
    expect(component['isAddOnAllowed']('Pos')).toBe(true);
  });
});








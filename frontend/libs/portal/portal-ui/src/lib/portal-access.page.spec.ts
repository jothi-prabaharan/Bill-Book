import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PortalAccessPage } from './portal-access.page';
import { PortalSession } from './portal-session';
import { ActivatedRoute, Router } from '@angular/router';

describe('PortalAccessPage (TK-132)', () => {
  let component: PortalAccessPage;
  let fixture: ComponentFixture<PortalAccessPage>;
  let mockSession: any;
  let mockRouter: any;
  let mockRoute: any;

  beforeEach(async () => {
    mockSession = {
      start: jest.fn(),
      app: jest.fn(),
    };
    mockRouter = {
      navigateByUrl: jest.fn(),
    };
    mockRoute = {
      snapshot: {
        paramMap: {
          get: jest.fn().mockReturnValue('abc-123'),
        },
      },
    };

    await TestBed.configureTestingModule({
      imports: [PortalAccessPage],
      providers: [
        { provide: PortalSession, useValue: mockSession },
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockRoute },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalAccessPage);
    component = fixture.componentInstance;
  });

  it('exchanges the code and navigates to the portal dashboard', async () => {
    mockSession.start.mockResolvedValue(true);
    mockSession.app.mockReturnValue('RetailErp');

    fixture.detectChanges();
    await fixture.whenStable();

    expect(mockSession.start).toHaveBeenCalledWith('abc-123');
    expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/portal/dashboard', { replaceUrl: true });
  });

  it('navigates to the expired page if the code is invalid', async () => {
    mockSession.start.mockResolvedValue(false);

    fixture.detectChanges();
    await fixture.whenStable();

    expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/portal/expired', { replaceUrl: true });
  });
});

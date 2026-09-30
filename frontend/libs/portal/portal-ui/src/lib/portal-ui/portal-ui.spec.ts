import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PortalUi } from './portal-ui';

describe('PortalUi', () => {
  let component: PortalUi;
  let fixture: ComponentFixture<PortalUi>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PortalUi],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalUi);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});


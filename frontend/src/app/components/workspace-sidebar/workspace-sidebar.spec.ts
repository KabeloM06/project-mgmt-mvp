import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WorkspaceSidebarComponent } from './workspace-sidebar';

describe('WorkspaceSidebar', () => {
  let component: WorkspaceSidebarComponent;
  let fixture: ComponentFixture<WorkspaceSidebarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [WorkspaceSidebarComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(WorkspaceSidebarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

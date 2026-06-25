import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WorkspaceSidebar } from './workspace-sidebar';

describe('WorkspaceSidebar', () => {
  let component: WorkspaceSidebar;
  let fixture: ComponentFixture<WorkspaceSidebar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkspaceSidebar]
    })
    .compileComponents();

    fixture = TestBed.createComponent(WorkspaceSidebar);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

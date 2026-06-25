import { TestBed } from '@angular/core/testing';

import { Workspace } from './workspace';

describe('Workspace', () => {
  let service: Workspace;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Workspace);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});

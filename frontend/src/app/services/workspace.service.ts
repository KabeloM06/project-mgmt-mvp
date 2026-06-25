import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Workspace, WorkspaceResponse } from '../models/project.models';

@Injectable({
  providedIn: 'root'
})
export class WorkspaceService {
  // Base endpoint pointing to our Day 4 .NET controller routes
  private apiUrl = '/api/workspaces';

  constructor(private http: HttpClient) {}

  // 1. GET: /api/workspaces -> Fetches all workspace documents for the sidebar panel
  getWorkspaces(): Observable<Workspace[]> {
    return this.http.get<Workspace[]>(this.apiUrl);
  }

  // 2. GET: /api/workspaces/{id} -> Fetches specific workspace details along with its cached tasks array
  getWorkspaceDetails(workspaceId: string): Observable<WorkspaceResponse> {
    return this.http.get<WorkspaceResponse>(`${this.apiUrl}/${workspaceId}`);
  }
}
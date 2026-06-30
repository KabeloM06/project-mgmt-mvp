import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Workspace, WorkspaceResponse } from '../models/project.models';
import { environment } from '../../environments/environment'; // 1. Import your environment configuration

@Injectable({
  providedIn: 'root'
})
export class WorkspaceService {
  // 2. Dynamically pull the base URL from your environment file
  private workspacesUrl = `${environment.apiUrl}/workspaces`;
  private documentsUrl = `${environment.apiUrl}/documents`;

  constructor(private http: HttpClient) {}

  // 3. GET: Fetch all workspace documents for the sidebar panel
  getWorkspaces(): Observable<Workspace[]> {
    return this.http.get<Workspace[]>(this.workspacesUrl);
  }

  // 4. GET: Fetch specific workspace details along with its cached tasks array
  getWorkspaceDetails(workspaceId: string): Observable<WorkspaceResponse> {
    return this.http.get<WorkspaceResponse>(`${this.workspacesUrl}/${workspaceId}`);
  }

  // 5. NEW DAY 7 ENDPOINT: Requests a secure, identity-backed Azure upload link
  getUploadUrl(workspaceId: string): Observable<{ uploadUrl: string, blobPath: string }> {
    return this.http.get<{ uploadUrl: string, blobPath: string }>(
      `${this.documentsUrl}/${workspaceId}/upload-url`
    );
  }
}
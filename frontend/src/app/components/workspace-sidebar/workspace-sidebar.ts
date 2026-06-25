import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WorkspaceService } from '../../services/workspace.service';
import { Workspace } from '../../models/project.models';

@Component({
  selector: 'app-workspace-sidebar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './workspace-sidebar.html',
  styleUrls: ['./workspace-sidebar.css']
})
export class WorkspaceSidebarComponent implements OnInit {
  workspaces: Workspace[] = [];
  activeWorkspaceId: string | null = null;

  // An event emitter to broadcast workspace selection changes up to the app layout shell
  @Output() workspaceSelected = new EventEmitter<Workspace>();

  constructor(private workspaceService: WorkspaceService) {}

  ngOnInit(): void {
    this.workspaceService.getWorkspaces().subscribe({
      next: (data) => {
        this.workspaces = data;
      },
      error: (err) => {
        console.error('Failed to populate workspace side panel roster:', err);
      }
    });
  }

  selectWorkspace(workspace: Workspace): void {
    this.activeWorkspaceId = workspace.workspaceId;
    this.workspaceSelected.emit(workspace);
  }
}
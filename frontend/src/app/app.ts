import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WorkspaceSidebarComponent } from './components/workspace-sidebar/workspace-sidebar';
import { KanbanBoardComponent } from './components/kanban-board/kanban-board';
import { Workspace } from './models/project.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, 
    WorkspaceSidebarComponent, 
    KanbanBoardComponent       
  ],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class AppComponent {
  title = 'project-management-mvp';
  
  // Holds the unified state context shared across our frontend UI slices
  activeWorkspace: Workspace | null = null;

  handleWorkspaceSelection(workspace: Workspace): void {
    this.activeWorkspace = workspace;
  }
}
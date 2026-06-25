import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WorkspaceService } from '../../services/workspace.service';
import { TaskItem } from '../../models/project.models';

@Component({
  selector: 'app-kanban-board',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './kanban-board.html',
  styleUrls: ['./kanban-board.css']
})
export class KanbanBoardComponent implements OnChanges {
  // Receives the partition context directly down-stream from the main layout shell
  @Input() workspaceId!: string;

  tasks: TaskItem[] = [];
  columns: ('To Do' | 'In Progress' | 'Done')[] = ['To Do', 'In Progress', 'Done'];

  constructor(private workspaceService: WorkspaceService) {}

  ngOnChanges(changes: SimpleChanges): void {
    // If the workspaceId property changes and contains a valid value, refresh the board state
    if (changes['workspaceId'] && this.workspaceId) {
      this.loadBoardData();
    }
  }

  private loadBoardData(): void {
    this.workspaceService.getWorkspaceDetails(this.workspaceId).subscribe({
      next: (data) => {
        this.tasks = data.tasks;
      },
      error: (err) => {
        console.error('Failed to resolve target workspace collection data:', err);
      }
    });
  }

  // Slices our raw state collection array by column workflow definitions
  getTasksByStatus(status: 'To Do' | 'In Progress' | 'Done'): TaskItem[] {
    return this.tasks.filter(task => task.status === status);
  }
}
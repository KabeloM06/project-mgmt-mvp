export interface TaskItem {
  id: string;
  workspaceId: string;
  type: 'task';
  title: string;
  status: 'To Do' | 'In Progress' | 'Done';
  tags: string[];
  assignedTo: string;
}

export interface WorkspaceResponse {
  workspaceId: string;
  tasks: TaskItem[];
}

export interface Workspace {
  id: string;
  workspaceId: string;
  type: 'workspace';
  name: string;
  description: string;
}
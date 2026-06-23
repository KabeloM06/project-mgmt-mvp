<h1>Azure Cosmos DB NoSQL Schema Design Decisions</h1>

<p><strong>Project:</strong> Cloud-Native Event-Driven Project Management Platform (MVP)</p>
<p><strong>Architecture Level:</strong> Data Storage & Access Tier</p>
<p><strong>Document Version:</strong> 1.0.0 (Day 2 - Commit 1)</p>

<hr>

<h2>1. Executive Architectural Summary</h2>
<p>
This document establishes the transactional data modeling strategy for the Project Management MVP utilizing <strong>Azure Cosmos DB (NoSQL API)</strong>. The primary objective is to maximize horizontal scalability and lower Request Unit (RU) consumption while remaining entirely within the Azure Always Free Tier allocation (1,000 RU/s throughput / 25 GB storage).
</p>

<h2>2. Core Database Configuration</h2>
<p>
To optimize cost boundaries under the free tier, the infrastructure utilizes a <strong>Shared Throughput Database</strong> allocation. Instead of provisioning isolated throughput per container (which minimizes resource efficiency), throughput is pooled at the database level.
</p>

<table border="1" cellpadding="6" style="border-collapse:collapse; width:100%; text-align:left;">
  <thead>
    <tr style="background-color: #f2f2f2;">
      <th>Database Parameter</th>
      <th>Configuration Setting</th>
      <th>Architectural Justification</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><strong>Throughput Provisioning Mode</strong></td>
      <td>Shared Manual Throughput</td>
      <td>Allows multiple containers to dynamically share the 400–1000 RU/s boundary without individual provisioning overhead.</td>
    </tr>
    <tr>
      <td><strong>API Type</strong></td>
      <td>NoSQL (Core API)</td>
      <td>Native engine support, deep .NET SDK integration, and optimal execution performance for point reads.</td>
    </tr>
  </tbody>
</table>

<p>&nbsp;</p>

<h2>3. Container Layout & Partition Key Strategy</h2>
<p>
Choosing correct partition keys is a fundamental requirement of the <strong>AZ-204 exam</strong> and real-world NoSQL engineering. To balance access patterns, the architecture is split into exactly two containers:
</p>

<h3>Container A: <code>WorkspacesAndTasks</code></h3>
<ul>
  <li><strong>Partition Key:</strong> <code>/workspaceId</code></li>
  <li><strong>Strategy:</strong> Multi-entity co-location (Polymorphic data structure).</li>
  <li><strong>Justification:</strong> The primary access pattern of a Jira or ClickUp clone is loading a single workspace's Kanban board or task list. By packing both the Workspace metadata document and all its underlying Tasks into the same partition key scope (<code>workspaceId</code>), fetches result in highly efficient <em>In-Partition Queries</em> that strike a single physical partition instead of scattering across the cluster.</li>
</ul>

<h3>Container B: <code>Users</code></h3>
<ul>
  <li><strong>Partition Key:</strong> <code>/id</code></li>
  <li><strong>Strategy:</strong> Isolated entity container.</li>
  <li><strong>Justification:</strong> Users exist independently of individual workspaces and frequently belong to multiple workspaces simultaneously. Partitioning users by a workspace ID would lead to massive data duplication or unindexed cross-partition lookups when validating a user profile session.</li>
</ul>

<p>&nbsp;</p>

<h2>4. JSON Document Schemas (Target Typology)</h2>

<h3>4.1 Workspace Metadata Document</h3>
<p>
Stored inside the <code>WorkspacesAndTasks</code> container. For this control document, the document's <code>id</code> is structurally identical to its <code>workspaceId</code> partition key.
</p>
<pre style="background-color: #f8f9fa; padding: 10px; border-left: 3px solid #007acc;">
{
  "id": "workspace-123",
  "workspaceId": "workspace-123",
  "type": "workspace",
  "name": "Engineering Team",
  "createdAt": "2026-06-23T10:00:00Z",
  "ownerId": "user-001"
}</pre>

<h3>4.2 Task Item Document</h3>
<p>
Stored inside the <code>WorkspacesAndTasks</code> container. Denormalized to hold unbounded lightweight child arrays (sub-tasks).
</p>
<pre style="background-color: #f8f9fa; padding: 10px; border-left: 3px solid #007acc;">
{
  "id": "task-456",
  "workspaceId": "workspace-123",
  "type": "task",
  "title": "Configure Managed Identities via Bicep",
  "status": "In Progress",
  "tags": ["backend", "security"],
  "assignedTo": "user-002",
  "subTasks": [
    { "title": "Write main.bicep", "isCompleted": true },
    { "title": "Test DefaultAzureCredential locally", "isCompleted": false }
  ]
}</pre>

<h3>4.3 User Document</h3>
<p>
Stored inside the <code>Users</code> container. Maps membership across the platform ecosystem.
</p>
<pre style="background-color: #f8f9fa; padding: 10px; border-left: 3px solid #007acc;">
{
  "id": "user-002",
  "email": "dev@example.com",
  "displayName": "Jane Doe",
  "associatedWorkspaces": ["workspace-123", "workspace-789"]
}</pre>

<p>&nbsp;</p>

<h2>5. Access Pattern Mapping (RU Optimization)</h2>

<table border="1" cellpadding="6" style="border-collapse:collapse; width:100%; text-align:left;">
  <thead>
    <tr style="background-color: #f2f2f2;">
      <th>Application Query/Access Pattern</th>
      <th>Target Container</th>
      <th>Execution Mode</th>
      <th>Cost / Performance Profile</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Get Task Details by ID</td>
      <td><code>WorkspacesAndTasks</code></td>
      <td>Point Read (Provide <code>id</code> + <code>PartitionKey</code>)</td>
      <td>Optimal (1 RU flat execution cost).</td>
    </tr>
    <tr>
      <td>Load entire Kanban Board / Workspace List</td>
      <td><code>WorkspacesAndTasks</code></td>
      <td>In-Partition Filter (<code>WHERE c.type = 'task'</code>)</td>
      <td>Highly Scalable (Scoped entirely to one partition).</td>
    </tr>
    <tr>
      <td>Fetch User Profile during Authentication</td>
      <td><code>Users</code></td>
      <td>Point Read (Provide <code>id</code> as PartitionKey)</td>
      <td>Optimal (1 RU flat execution cost).</td>
    </tr>
  </tbody>
</table>
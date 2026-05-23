import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { DataTable, type Column } from './DataTable';

interface TestRow {
  id: string;
  name: string;
  email: string;
}

const columns: Column<TestRow>[] = [
  { key: 'name', header: 'Name', render: (row) => row.name },
  { key: 'email', header: 'Email', render: (row) => row.email },
];

const sampleData: TestRow[] = [
  { id: '1', name: 'Alice', email: 'alice@example.com' },
  { id: '2', name: 'Bob', email: 'bob@example.com' },
  { id: '3', name: 'Charlie', email: 'charlie@example.com' },
];

describe('DataTable', () => {
  it('renders column headers', () => {
    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={3}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
      />
    );

    expect(screen.getByText('Name')).toBeInTheDocument();
    expect(screen.getByText('Email')).toBeInTheDocument();
  });

  it('renders data rows', () => {
    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={3}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
      />
    );

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('shows empty message when no data', () => {
    render(
      <DataTable
        columns={columns}
        data={[]}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={0}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
        emptyMessage="No customers found"
      />
    );

    expect(screen.getByText('No customers found')).toBeInTheDocument();
  });

  it('shows loading state', () => {
    render(
      <DataTable
        columns={columns}
        data={[]}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={0}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
        isLoading={true}
      />
    );

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('displays correct pagination info', () => {
    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={25}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
      />
    );

    expect(screen.getByText('1–10 of 25')).toBeInTheDocument();
    expect(screen.getByText('1 / 3')).toBeInTheDocument();
  });

  it('calls onPageChange when next page is clicked', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();

    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={25}
        onPageChange={onPageChange}
        onPageSizeChange={() => {}}
      />
    );

    await user.click(screen.getByLabelText('Next page'));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('calls onPageSizeChange when page size is changed', async () => {
    const user = userEvent.setup();
    const onPageSizeChange = vi.fn();

    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={25}
        onPageChange={() => {}}
        onPageSizeChange={onPageSizeChange}
      />
    );

    await user.selectOptions(screen.getByLabelText('Rows per page:'), '25');
    expect(onPageSizeChange).toHaveBeenCalledWith(25);
  });

  it('disables previous/first buttons on first page', () => {
    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={1}
        pageSize={10}
        totalItems={25}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
      />
    );

    expect(screen.getByLabelText('First page')).toBeDisabled();
    expect(screen.getByLabelText('Previous page')).toBeDisabled();
  });

  it('disables next/last buttons on last page', () => {
    render(
      <DataTable
        columns={columns}
        data={sampleData}
        getRowKey={(row) => row.id}
        currentPage={3}
        pageSize={10}
        totalItems={25}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
      />
    );

    expect(screen.getByLabelText('Next page')).toBeDisabled();
    expect(screen.getByLabelText('Last page')).toBeDisabled();
  });
});

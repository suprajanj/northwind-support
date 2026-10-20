import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
    getEscalations: vi.fn(),
  },
}));

describe('EscalationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]);
  });

  it('blocks submission with short reason (<5 characters) and does not call API', async () => {
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });
    const onEscalated = vi.fn();

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const textarea = screen.getByPlaceholderText(/State clearly why this ticket must be escalated/i);
    const submitBtn = screen.getByRole('button', { name: /Escalate Ticket/i });

    // Type only 3 characters
    await userEvent.type(textarea, 'bad');
    fireEvent.click(submitBtn);

    expect(await screen.findByText(/Reason must be at least 5 characters long/i)).toBeInTheDocument();
    expect(ticketsApi.escalateTicket).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('displays 409 Conflict server error and leaves ticket unchanged', async () => {
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });
    const onEscalated = vi.fn();

    vi.mocked(ticketsApi.escalateTicket).mockRejectedValueOnce(
      new ApiError(409, 'Ticket TCK-0012 is Resolved and cannot be escalated.'),
    );

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const textarea = screen.getByPlaceholderText(/State clearly why this ticket must be escalated/i);
    const submitBtn = screen.getByRole('button', { name: /Escalate Ticket/i });

    await userEvent.type(textarea, 'Valid escalation reason provided here');
    fireEvent.click(submitBtn);

    expect(await screen.findByText('Ticket TCK-0012 is Resolved and cannot be escalated.')).toBeInTheDocument();
    expect(ticketsApi.escalateTicket).toHaveBeenCalledTimes(1);
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('calls API and onEscalated callback on valid escalation', async () => {
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });
    const updatedTicket = { ...ticket, priority: 'High' as const };
    const onEscalated = vi.fn();

    vi.mocked(ticketsApi.escalateTicket).mockResolvedValueOnce(updatedTicket);

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const textarea = screen.getByPlaceholderText(/State clearly why this ticket must be escalated/i);
    const submitBtn = screen.getByRole('button', { name: /Escalate Ticket/i });

    await userEvent.type(textarea, 'Valid escalation reason for testing');
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(ticketsApi.escalateTicket).toHaveBeenCalledWith(
        ticket.id,
        expect.objectContaining({ reason: 'Valid escalation reason for testing' }),
      );
    });

    expect(onEscalated).toHaveBeenCalledWith(updatedTicket);
  });

  it('disables escalation when ticket is Critical', async () => {
    const ticket = ticketDetail({ priority: 'Critical', status: 'Open' });
    const onEscalated = vi.fn();

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    expect(screen.getByText(/Critical tickets are at the highest priority and cannot be escalated/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Escalate Ticket/i })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('no-escalations')).toBeInTheDocument());
  });

  it('disables escalation when ticket is Resolved or Closed', async () => {
    const ticket = ticketDetail({ priority: 'Medium', status: 'Resolved' });
    const onEscalated = vi.fn();

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    expect(screen.getByText(/Ticket is Resolved and cannot be escalated/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Escalate Ticket/i })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('no-escalations')).toBeInTheDocument());
  });
});

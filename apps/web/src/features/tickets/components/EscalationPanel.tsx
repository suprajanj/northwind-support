import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail, TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (updated: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('team-lead@northwind.com');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [escalations, setEscalations] = useState<TicketEscalation[]>([]);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);

  const isCritical = ticket.priority === 'Critical';
  const isClosedOrResolved = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const canEscalate = !isCritical && !isClosedOrResolved;

  const cannotEscalateReason = isCritical
    ? 'Critical tickets are at the highest priority and cannot be escalated.'
    : isClosedOrResolved
      ? `Ticket is ${ticket.status} and cannot be escalated.`
      : null;

  const loadHistory = useCallback(async () => {
    setIsLoadingHistory(true);
    try {
      const data = await ticketsApi.getEscalations(ticket.id);
      setEscalations(data);
    } catch {
      // History failure is non-fatal to the main ticket view
    } finally {
      setIsLoadingHistory(false);
    }
  }, [ticket.id]);

  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setValidationError(null);
    setApiError(null);

    const trimmed = reason.trim();
    if (trimmed.length < 5) {
      setValidationError('Reason must be at least 5 characters long.');
      return;
    }
    if (trimmed.length > 500) {
      setValidationError('Reason cannot exceed 500 characters.');
      return;
    }

    if (!canEscalate) {
      setValidationError(cannotEscalateReason ?? 'This ticket cannot be escalated.');
      return;
    }

    setIsSubmitting(true);
    try {
      const updated = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmed,
        escalatedBy: escalatedBy.trim() || undefined,
      });
      setReason('');
      onEscalated(updated);
      await loadHistory();
    } catch (err) {
      if (err instanceof ApiError) {
        setApiError(err.message);
      } else {
        setApiError('An unexpected error occurred while escalating the ticket.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card escalation-panel" data-testid="escalation-panel">
      <h2>Escalation</h2>

      {cannotEscalateReason ? (
        <div className="notice notice--info" role="status" data-testid="escalate-disabled-notice">
          <p>{cannotEscalateReason}</p>
        </div>
      ) : (
        <form onSubmit={handleSubmit} noValidate>
          {apiError && (
            <div className="notice notice--danger" role="alert" data-testid="escalate-api-error">
              <p>{apiError}</p>
            </div>
          )}

          <div className="field">
            <label className="field__label" htmlFor="escalation-reason">
              Reason for escalation <span className="field__required">*</span>
            </label>
            <textarea
              id="escalation-reason"
              rows={3}
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (validationError) setValidationError(null);
              }}
              placeholder="State clearly why this ticket must be escalated (5-500 characters)..."
              disabled={isSubmitting || !canEscalate}
              aria-invalid={Boolean(validationError)}
              aria-describedby={validationError ? 'escalation-reason-error' : undefined}
            />
            {validationError && (
              <span id="escalation-reason-error" className="field__error" role="alert" data-testid="escalate-validation-error">
                {validationError}
              </span>
            )}
            <span className="field__hint">
              {reason.trim().length}/500 characters (min. 5)
            </span>
          </div>

          <div className="field">
            <label className="field__label" htmlFor="escalated-by">
              Escalated by
            </label>
            <input
              id="escalated-by"
              type="text"
              value={escalatedBy}
              onChange={(e) => setEscalatedBy(e.target.value)}
              disabled={isSubmitting || !canEscalate}
            />
          </div>

          <div className="button-row">
            <button
              type="submit"
              className="button button--primary"
              disabled={isSubmitting || !canEscalate}
              data-testid="escalate-submit-btn"
            >
              {isSubmitting ? 'Escalating...' : 'Escalate Ticket'}
            </button>
          </div>
        </form>
      )}

      <div className="escalation-history" style={{ marginTop: '1.5rem' }}>
        <h3>Escalation History</h3>
        {isLoadingHistory && <p className="summary__note">Loading history...</p>}
        {!isLoadingHistory && escalations.length === 0 && (
          <p className="summary__note" data-testid="no-escalations">No escalations recorded for this ticket.</p>
        )}
        {!isLoadingHistory && escalations.length > 0 && (
          <ul className="escalation-list" data-testid="escalation-list" style={{ listStyle: 'none', padding: 0 }}>
            {escalations.map((item) => (
              <li
                key={item.id}
                className="escalation-item"
                style={{
                  borderTop: '1px solid var(--border, #e5e7eb)',
                  paddingTop: '0.75rem',
                  marginTop: '0.75rem',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span>
                    Priority: <PriorityBadge priority={item.fromPriority} /> → <PriorityBadge priority={item.toPriority} />
                  </span>
                  <span className="summary__note">{formatDateTime(item.escalatedAtUtc)}</span>
                </div>
                <div style={{ fontSize: '0.875rem', marginTop: '0.25rem' }}>
                  <strong>Owner:</strong> {item.fromAgent?.fullName ?? 'Unassigned'} → {item.toAgent?.fullName ?? 'Unassigned'}
                </div>
                {item.toDueAtUtc && (
                  <div style={{ fontSize: '0.875rem' }}>
                    <strong>New SLA Due:</strong> {formatDateTime(item.toDueAtUtc)}
                  </div>
                )}
                <div style={{ fontSize: '0.875rem', marginTop: '0.25rem', fontStyle: 'italic' }}>
                  "{item.reason}"
                </div>
                <div className="summary__note" style={{ fontSize: '0.75rem' }}>
                  Escalated by {item.escalatedBy}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

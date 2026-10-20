import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { PriorityBadge } from './components/TicketBadges';
import { SlaIndicator } from './components/SlaIndicator';
import { CreateTicketForm } from './components/CreateTicketForm';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card" role="status" data-testid="triage-result">
          <div className="notice notice--info" style={{ marginBottom: '1.25rem' }}>
            <p>
              Successfully raised <strong>{created.reference}</strong> for {created.customer.name}.
            </p>
          </div>

          <div style={{ marginBottom: '1.5rem' }}>
            <h2 style={{ fontSize: '1.125rem', marginBottom: '0.75rem' }}>Automatic Triage Decision</h2>
            <dl className="summary" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))' }}>
              <div>
                <dt>Priority</dt>
                <dd>
                  <PriorityBadge priority={created.priority} />
                  {created.triage?.priorityReason && (
                    <span className="summary__note">{created.triage.priorityReason}</span>
                  )}
                </dd>
              </div>

              <div>
                <dt>SLA Window & Due Date</dt>
                <dd>
                  <SlaIndicator slaStatus={created.slaStatus} dueAtUtc={created.dueAtUtc} showDue={true} />
                  {created.triage?.slaWindowReason && (
                    <span className="summary__note">{created.triage.slaWindowReason}</span>
                  )}
                </dd>
              </div>

              <div>
                <dt>Assignment</dt>
                <dd>
                  <strong>{created.assignedAgent?.fullName ?? 'Unassigned'}</strong>
                  {created.triage?.assignmentReason && (
                    <span className="summary__note">{created.triage.assignmentReason}</span>
                  )}
                </dd>
              </div>
            </dl>
          </div>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}

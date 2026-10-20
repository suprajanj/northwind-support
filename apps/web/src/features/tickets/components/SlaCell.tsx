import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** The due date and where the ticket stands against it, as shown in the ticket list and detail page. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  return <SlaIndicator slaStatus={slaStatus} dueAtUtc={dueAtUtc} showDue={true} />;
}

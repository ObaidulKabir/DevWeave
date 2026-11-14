import type { PlanStatusCampaign } from './types';

/**
 * Campaigns that are still live (active or proposed) on a plan version that is no longer current.
 * Suspended and completed ones are already handled, so they are not counted. Every screen that talks about
 * "campaigns still on an older version" must use this one definition so the numbers always agree.
 */
export function campaignsStillOnOlderPlan(campaigns: PlanStatusCampaign[] | null | undefined): PlanStatusCampaign[] {
  return (campaigns ?? []).filter((c) => !c.onCurrentPlan && (c.status === 'Active' || c.status === 'Proposed'));
}

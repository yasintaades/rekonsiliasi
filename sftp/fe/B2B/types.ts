export interface Detail {
  refNo: string;
  marketplace: string | null;
  itemName: string | null;
  skuAnchanto: string | null;
  qtyAnchanto: number | null;
  dateAnchanto: string | null;
  senderSite: string | null;
  receiveSite: string | null;
  skuCegid: string | null;
  itemNameCegid: string | null;
  qtyCegid: number | null;
  dateCegid: string | null;
  unitCOGS: number | null;
  status: string;
}

export interface ReconSummary {
  match: number;
  onlyAnchanto: number;
  onlyCegid: number;
  mismatch: number;
}

export interface ReconResult {
  details: Detail[];
  summary: ReconSummary;
  reconciliationId: number;
}
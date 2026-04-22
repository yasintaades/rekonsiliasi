export interface Detail {
  refNo: string;
  senderSite: string | null;
  receiveSite: string | null;
  itemNameTransfer: string | null;
  unitCOGS: number | null;
  skuTransfer: string | null;
  qtyTransfer: number | null;
  dateTransfer: string | null;
  consignmentNo: string | null;
  skuConsignment: string | null;
  qtyConsignment: number | null;
  dateConsignment: string | null;
  senderSiteReceived: string | null;
  receiveSiteReceived: string | null;
  itemNameReceived: string | null;
  unitCOGSReceived: number | null;
  skuReceived: string | null;
  qtyReceived: number | null;
  dateReceived: string | null;
  status: string;
}

export interface SftpLog {
  id: number;
  fileName: string;
  status: string;
}

export interface ReconResult {
  details: Detail[];
  summary: {
    all: number;
    complete: number;
    mismatch: number;
  };
  reconciliationId: number;
}
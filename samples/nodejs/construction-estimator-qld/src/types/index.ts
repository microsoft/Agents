// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

export interface ConversationState {
  count: number;
  currentEstimate?: EstimateSession;
  settings: EstimatorSettings;
}

export interface EstimateSession {
  id: string;
  clientName?: string;
  projectName?: string;
  version: string;
  createdAt: Date;
  measurements: Measurement[];
  assemblies: Assembly[];
  estimate?: Estimate;
  pdfMetadata?: PDFMetadata;
}

export interface EstimatorSettings {
  verbosity: 'summary' | 'full' | 'quick';
  grouping: 'by_trade' | 'by_CSI' | 'flat';
  showLegend: boolean;
  profitMargin: number;
  contingency: number;
  wastage: number;
}

export interface Measurement {
  id: string;
  type: 'length' | 'area' | 'count' | 'volume';
  value: number;
  unit: string;
  description: string;
  location?: string;
  sheetRef?: string;
}

export interface Assembly {
  id: string;
  code: string;
  description: string;
  trade: TradeType;
  quantity: number;
  unit: string;
  materialRate: number;
  labourRate: number;
  wastage: number;
  productivityFactor: number;
  materials: Material[];
}

export type TradeType = 
  | 'PLASTERING' 
  | 'PARTITIONS' 
  | 'CEILINGS' 
  | 'CARPENTRY' 
  | 'STEEL_FRAMING';

export interface Material {
  name: string;
  quantity: number;
  unit: string;
  rate: number;
  supplier?: string;
}

export interface Estimate {
  id: string;
  summary: EstimateSummary;
  lineItems: LineItem[];
  siteFactors: SiteFactors;
  assumptions: string[];
  alternates?: Alternate[];
}

export interface EstimateSummary {
  subtotal: number;
  profit: number;
  contingency: number;
  siteFactors: number;
  total: number;
  totalByTrade: Record<TradeType, number>;
}

export interface LineItem {
  itemNumber: string;
  description: string;
  trade: TradeType;
  quantity: number;
  unit: string;
  materialRate: number;
  labourRate: number;
  totalRate: number;
  totalCost: number;
  notes?: string;
}

export interface SiteFactors {
  travelDistance?: number;
  travelCost: number;
  wetWeatherRisk: number;
  accessComplexity: 'easy' | 'moderate' | 'difficult';
  accessFactor: number;
  totalSiteFactors: number;
}

export interface Alternate {
  id: string;
  description: string;
  costDifference: number;
  notes: string;
}

export interface PDFMetadata {
  fileName: string;
  pageCount: number;
  scale?: string;
  scaleDetectionMethod?: 'embedded' | 'inferred' | 'manual';
  sheets: string[];
}

export interface QLDRateData {
  materials: Record<string, MaterialRate>;
  labour: Record<string, LabourRate>;
  assemblies: Record<string, AssemblyTemplate>;
}

export interface MaterialRate {
  name: string;
  unit: string;
  rate: number;
  supplier: string;
  lastUpdated: string;
}

export interface LabourRate {
  trade: TradeType;
  description: string;
  ratePerHour: number;
  productivityUnit: string;
  unitsPerHour: number;
}

export interface AssemblyTemplate {
  code: string;
  description: string;
  trade: TradeType;
  unit: string;
  materials: {
    materialCode: string;
    quantityPerUnit: number;
  }[];
  labourHoursPerUnit: number;
  wastage: number;
  notes?: string;
}

export interface ExportOptions {
  format: 'csv' | 'json' | 'text';
  includeMetadata: boolean;
  includeAssumptions: boolean;
  groupBy?: 'trade' | 'CSI';
}

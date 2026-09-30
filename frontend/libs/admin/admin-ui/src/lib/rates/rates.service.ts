import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ExchangeRateRow {
  exchangeRateId: number;
  fromCurrencyCode: string;
  toCurrencyCode: string;
  rateDate: string;
  rate: number;
  source: string;
}

export interface MetalRateRow {
  metalRateId: number;
  metal: string;
  purityCode: string;
  rateDate: string;
  ratePerGram: number;
  source: string;
}

export interface SaveExchangeRate {
  fromCurrencyCode: string;
  toCurrencyCode: string;
  rateDate: string;
  rate: number;
}

export interface SaveMetalRate {
  metal: string;
  purityCode: string;
  rateDate: string;
  ratePerGram: number;
}

/**
 * Master's `api/rates` — the global exchange and metal rate history (TK-24).
 * Reading the history needs `platform.view`, and entering or removing a rate
 * needs `platform.edit`, because every customer reads the same rows.
 */
@Injectable({ providedIn: 'root' })
export class RatesService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/rates';

  exchangeHistory(take = 100): Promise<ExchangeRateRow[]> {
    const params = new HttpParams().set('take', take);
    return firstValueFrom(this.http.get<ExchangeRateRow[]>(`${this.base}/exchange/history`, { params }));
  }

  metalHistory(take = 100): Promise<MetalRateRow[]> {
    const params = new HttpParams().set('take', take);
    return firstValueFrom(this.http.get<MetalRateRow[]>(`${this.base}/metal/history`, { params }));
  }

  saveExchange(rate: SaveExchangeRate): Promise<unknown> {
    return firstValueFrom(this.http.put(`${this.base}/exchange`, rate));
  }

  saveMetal(rate: SaveMetalRate): Promise<unknown> {
    return firstValueFrom(this.http.put(`${this.base}/metal`, rate));
  }

  deleteExchange(id: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/exchange/${id}`));
  }

  deleteMetal(id: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/metal/${id}`));
  }
}

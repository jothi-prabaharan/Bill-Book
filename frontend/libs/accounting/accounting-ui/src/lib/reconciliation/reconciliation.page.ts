import { ChangeDetectionStrategy, Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  IonHeader,
  IonToolbar,
  IonTitle,
  IonContent,
  IonGrid,
  IonRow,
  IonCol,
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardContent,
  IonButton,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  ModalController
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { cloudUploadOutline, checkmarkCircleOutline } from 'ionicons/icons';
import { StatementUploadFormComponent } from './statement-upload-form.component';

interface LedgerMatch {
  journalLedgerId: number;
  ledgerDate: string;
  transactionTypeCode: string;
  amount: number;
  description: string;
  score: number;
}

interface StatementLine {
  bankStatementLineId: number;
  transactionDate: string;
  description: string;
  referenceNo: string;
  amount: number;
  suggestedMatches: LedgerMatch[];
  isReconciled?: boolean;
}

@Component({
  selector: 'bb-reconciliation-page',
  standalone: true,
  imports: [
    CommonModule,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonGrid,
    IonRow,
    IonCol,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardContent,
    IonButton,
    IonIcon,
    IonItem,
    IonLabel,
    IonList
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>Bank Reconciliation</ion-title>
        <ion-button slot="end" fill="clear" (click)="openUploadModal()">
          <ion-icon slot="start" name="cloud-upload-outline"></ion-icon>
          Upload Statement
        </ion-button>
      </ion-toolbar>
    </ion-header>
    
    <ion-content class="ion-padding">
      <ion-grid>
        <ion-row>
          <!-- Left Pane: Bank Statement Lines -->
          <ion-col size="12" size-md="6">
            <ion-list>
              <ion-item *ngFor="let line of lines()" [color]="line.isReconciled ? 'success' : ''">
                <ion-label>
                  <h2>{{ line.transactionDate | date }}</h2>
                  <h3>{{ line.description }}</h3>
                  <p>Ref: {{ line.referenceNo }}</p>
                </ion-label>
                <ion-label slot="end" class="ion-text-right">
                  <h2>{{ line.amount | currency:'INR' }}</h2>
                </ion-label>
              </ion-item>
            </ion-list>
          </ion-col>
          
          <!-- Right Pane: Suggested Ledger Matches -->
          <ion-col size="12" size-md="6">
            <div *ngFor="let line of lines()">
              <ion-card *ngIf="!line.isReconciled">
                <ion-card-header>
                  <ion-card-title>Matches for {{ line.amount | currency:'INR' }}</ion-card-title>
                </ion-card-header>
                <ion-card-content>
                  <div *ngIf="line.suggestedMatches.length === 0">No matches found.</div>
                  <ion-item *ngFor="let match of line.suggestedMatches">
                    <ion-label>
                      <h3>{{ match.transactionTypeCode }} - {{ match.ledgerDate | date }}</h3>
                      <p>{{ match.description }}</p>
                    </ion-label>
                    <ion-button slot="end" (click)="reconcile(line, match)">
                      Match
                    </ion-button>
                  </ion-item>
                </ion-card-content>
              </ion-card>
            </div>
          </ion-col>
        </ion-row>
      </ion-grid>
    </ion-content>
  `
})
export class ReconciliationPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly modalCtrl = inject(ModalController);

  readonly lines = signal<StatementLine[]>([]);
  // Use a hardcoded statement ID for demonstration, 
  // or set it dynamically after upload.
  readonly currentStatementId = signal<number>(1);

  constructor() {
    addIcons({ cloudUploadOutline, checkmarkCircleOutline });
  }

  ngOnInit() {
    void this.loadSuggestions();
  }

  async loadSuggestions() {
    this.http.get<StatementLine[]>(`/api/reconciliation/${this.currentStatementId()}/suggestions`)
      .subscribe(res => {
        this.lines.set(res || []);
      });
  }

  reconcile(line: StatementLine, match: LedgerMatch) {
    this.http.post('/api/reconciliation/reconcile', {
      bankStatementLineId: line.bankStatementLineId,
      journalLedgerId: match.journalLedgerId
    }).subscribe(() => {
      // Mark as visually reconciled
      this.lines.update(all => all.map(l => 
        l.bankStatementLineId === line.bankStatementLineId 
          ? { ...l, isReconciled: true } 
          : l
      ));
    });
  }

  async openUploadModal() {
    const modal = await this.modalCtrl.create({
      component: StatementUploadFormComponent
    });
    await modal.present();
    
    const { data } = await modal.onWillDismiss();
    if (data?.bankStatementId) {
      this.currentStatementId.set(data.bankStatementId);
      void this.loadSuggestions();
    }
  }
}

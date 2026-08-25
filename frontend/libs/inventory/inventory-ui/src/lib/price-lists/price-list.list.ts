import { ChangeDetectionStrategy, Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'bb-price-list-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './price-list.list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriceListListComponent implements OnInit {
  private readonly http = inject(HttpClient);
  
  readonly priceLists = signal<any[]>([]);
  readonly newListName = signal<string>('');
  readonly newListDescription = signal<string>('');

  ngOnInit() {
    this.loadPriceLists();
  }

  loadPriceLists() {
    this.http.get<any[]>('/api/inventory/price-lists')
      .subscribe(lists => this.priceLists.set(lists));
  }

  createPriceList() {
    if (!this.newListName()) return;

    this.http.post<any>('/api/inventory/price-lists', { 
      name: this.newListName(), 
      description: this.newListDescription(),
      isActive: true 
    })
      .subscribe(res => {
        this.priceLists.update(lists => [...lists, res]);
        this.newListName.set('');
        this.newListDescription.set('');
      });
  }
}

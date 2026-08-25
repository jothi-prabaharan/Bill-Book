import { ChangeDetectionStrategy } from '@angular/core';
import { Component, input, forwardRef, signal, inject, OnInit } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-master-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MasterSelectComponent),
      multi: true
    }
  ],
  templateUrl: './master-select.component.html',
  styleUrl: './master-select.component.scss'
})
export class MasterSelectComponent implements ControlValueAccessor, OnInit {
  readonly masterType = input.required<'contact' | 'item' | 'account' | 'tax'>();
  readonly label = input<string>();
  
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);

  options = signal<any[]>([]);
  innerValue = signal<any>(null);
  disabled = signal(false);

  private onChange: (val: any) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnInit() {
    void this.loadOptions().then(() => {
      // Auto-select if we just returned from creating this master
      const returnMaster = this.route.snapshot.queryParamMap.get('returnMaster');
      const returnId = this.route.snapshot.queryParamMap.get('returnId');
      
      if (returnMaster === this.masterType() && returnId) {
        const id = parseInt(returnId, 10);
        if (!isNaN(id)) {
          this.innerValue.set(id);
          this.onChange(id);
          
          // Clean up the URL so refreshing doesn't keep selecting it
          const url = new URL(window.location.href);
          url.searchParams.delete('returnMaster');
          url.searchParams.delete('returnId');
          this.location.replaceState(url.pathname + url.search);
        }
      }
    });
  }

  async loadOptions() {
    let endpoint = '';
    switch (this.masterType()) {
      case 'contact': endpoint = '/api/contacts?includeInactive=false'; break;
      case 'item': endpoint = '/api/items?includeInactive=false'; break;
      case 'account': endpoint = '/api/accounts?includeInactive=false'; break;
      case 'tax': endpoint = '/api/tax-masters?includeHistory=false'; break;
    }
    
    if (endpoint) {
      try {
        const data = await this.http.get<any[]>(endpoint).toPromise();
        this.options.set(data || []);
      } catch (e) {
        console.error('Failed to load master options', e);
      }
    }
  }

  getOptionLabel(opt: any): string {
    switch (this.masterType()) {
      case 'contact': return opt.displayName || opt.contactCode;
      case 'item': return opt.itemName || opt.itemCode;
      case 'account': return opt.accountName || opt.accountCode;
      case 'tax': return opt.taxName;
      default: return opt.name || opt.id;
    }
  }

  getOptionValue(opt: any): any {
    switch (this.masterType()) {
      case 'contact': return opt.contactId;
      case 'item': return opt.itemId;
      case 'account': return opt.accountId;
      case 'tax': return opt.taxMasterId;
      default: return opt.id;
    }
  }

  writeValue(val: any): void {
    this.innerValue.set(val);
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  onSelect(event: Event) {
    const val = (event.target as HTMLSelectElement).value;
    const num = parseInt(val, 10);
    const finalVal = isNaN(num) ? null : num;
    this.innerValue.set(finalVal);
    this.onChange(finalVal);
    this.onTouched();
  }

  onCreateClick() {
    let routePath = '';
    switch (this.masterType()) {
      case 'contact': routePath = '/contacts'; break;
      case 'item': routePath = '/inventory/items'; break;
      case 'account': routePath = '/accounting/chart-of-accounts'; break;
      case 'tax': routePath = '/settings/tax'; break;
    }
    
    if (routePath) {
      // Pass the current URL as returnUrl, and action=create
      void this.router.navigate([routePath], { 
        queryParams: { 
          action: 'create', 
          returnUrl: this.router.url.split('?')[0], // base url to return to
          returnMaster: this.masterType()
        } 
      });
    }
  }
}


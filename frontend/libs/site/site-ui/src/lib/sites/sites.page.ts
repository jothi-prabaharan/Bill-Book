import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SiteApiService, SiteDto } from '@bill-book/site-core';

@Component({
  selector: 'bb-sites-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sites.page.html',
  styleUrl: './sites.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SitesPage implements OnInit {
  private readonly siteApi = inject(SiteApiService);

  readonly sites = signal<SiteDto[]>([]);
  readonly loading = signal<boolean>(false);

  async ngOnInit(): Promise<void> {
    await this.loadSites();
  }

  async loadSites(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await this.siteApi.list();
      this.sites.set(data);
    } catch {
      this.sites.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}

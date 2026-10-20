# Archetypes in ECommerceApp: what to take and what to leave

- **Date:** 2026-10-20
- **Status:** analysis and recommendation. This is not an implementation plan.
- **Principle:** take individual building blocks that solve our problems. Do not port whole modules 1:1.

---

## 1. Summary

| Archetype | Verdict | Key building block |
|---|---|---|
| Product catalog | **Yes** | product features with value constraints |
| Pricing (price list and price components) | **Yes** | time-versioned prices and a price breakdown (net, VAT, discount, fees) |
| Accounting (settlement ledger) | **Later** | who owes whom how much; a refund as a reversal entry |
| Rules (conditions, discounts) | **Partly in place** | rule result with a reason and a severity (blocks / warns) |
| Quantity / Money | **Partly** | units of measure, an amount that allows zero and negative values in components |
| Party (roles, relationships) | **Later** | customer roles (B2B buyer, approver), customer segments |
| Plan vs Execution | **Later** | compare a sales plan against actual results |
| Inventory (batches, serial numbers) | **Not now** | not needed: we sell interchangeable pieces |
| Ordering, Graphs | **Not now** | our modules already cover this or the problem does not occur |

The key boundary to keep:

```
Catalog     ->  WHAT we sell and TO WHOM / WHERE it may be sold
Pricing     ->  HOW MUCH it costs and WHAT the amount consists of
Accounting  ->  WHO owes WHOM after the sale and how it was settled
```

The catalog does not calculate prices. Pricing does not post entries. The ledger does not decide prices.

---

## 2. Current state (starting point)

| Area | What exists today | What is missing |
|---|---|---|
| Product | name, description, one price, status, category, tags, up to 5 images | features (colour, size, weight), value constraints, variants, bundles, sales conditions, sales period |
| Product price | one amount without currency | time versions, quantity tiers, net / VAT split |
| Order | order total, optional discount percentage, line unit price frozen at checkout | price breakdown into components, information on what the discount comes from |
| Coupons | rule pipeline (scope, discount, constraints), result: passed / failed + total reduction | a reduction assigned to a specific rule and line |
| Taxes, commissions, partner fees, margin | none | all of it |
| Payment | total amount, currency, statuses | amount split, payment provider fee |
| Refund | lines, reason, status | refund amount and its split (net / VAT) |
| Inventory | quantity and reservations per product | batches, serial numbers (not needed today) |
| Products without stock | a "digital" flag exists only in inventory, the catalog always publishes "physical", the cart requires stock | a sales mode in the catalog; today an e-book or a training cannot be sold without faking stock (section 8.2) |

Two facts shape every decision below:

1. **The order price is frozen when checkout starts.** This is a good rule and it stays. Pricing runs before the order exists; after the order is placed we store the result instead of recalculating it.
2. **The shared money type allows only positive amounts.** A price component can be zero (e.g. 0% VAT) or negative (a discount). This must be decided before modelling price components (a separate type or a change to the shared type's rules).

---

## 3. Product catalog

### What to take

| Building block | What it gives us | Example |
|---|---|---|
| **Feature type with a constraint** | a feature has a name, a value type and a rule: allowed values, numeric range, date range, text pattern | `colour` in {black, white}, `weight` 0.1–30 kg, `EAN` matching a pattern |
| **Required / optional feature** | a product type enforces complete data before publishing | shoes must have `size`, `material` is optional |
| **Product feature values** | describe a product in many ways without new columns or classes | filtering by features in the storefront |
| **Sales condition** | a product sold only in a given channel, country or to adults | Web only, Poland only, alcohol 18+ |
| **Sales period** | a seasonal product available within given dates | Christmas offer 1–31 December |
| **Result with a reason and a severity** | a rule returns a list of findings: **blocks** or **warns** (the customer confirms) | "missing size" blocks, "long delivery time" warns |
| **Rule attached to a feature value** | a specific value triggers an extra condition | choosing `engraving` adds 2 days of lead time |

### What to take later

| Building block | When it makes sense |
|---|---|
| Bundles (a product built from choices and rules) | when bundles appear, e.g. "laptop + bag + mouse, at most 2 accessories" |
| Product relationships (substitute, complement, requires, incompatible) | when we want "buy also" recommendations or to block incompatible cart lines |
| A separate "offer entry" (marketing name, categories, sales period) separated from the product definition | when the same product is sold in several offers. Today a sales period as a condition is enough |

### What not to do

- **Do not model variants as plain features when a variant has its own stock.** Shoe sizes with separate stock quantities are separate sellable units (variants), not just feature values. Decide this before starting, because it affects inventory, cart and orders.
- **Do not port tracking of individual units, batches and serial numbers.** We sell interchangeable pieces.
- **Do not add a generic "everything is a dictionary" without constraints.** The value of this block is constraints and validation, not free-form fields.

---

## 4. Pricing: price components

### What to take

| Building block | What it gives us | Example |
|---|---|---|
| **Named price component** | the amount is not one number but a tree of components | `net price`, `VAT 23%`, `coupon discount`, `delivery` |
| **Component depending on another** | VAT calculated from net, commission from the discounted amount | VAT = 23% × (net − discount) |
| **Result breakdown** | shows what the total consists of; can be shown to the customer and stored in the order | summary in the cart and in order details |
| **Simple calculators** | fixed amount, percentage of a base, quantity tiers | packaging fee 5 PLN, VAT 23%, cheaper from 10 pcs |
| **Time-versioned prices** | price change from a date, promotion for a period, automatic return to the base price | price 199 PLN, 149 PLN during Black Friday |
| **Component applicability condition** | a component applies only in a given context | free delivery above 200 PLN |
| **Unit price vs total price** | explicit distinction between price per piece and price per line | "3 for the price of 2" |

### How it connects to what we have

- **Coupons** already have a rule pipeline. The missing piece is for each rule to return **its own component** (name, line, amount) instead of one total reduction. A coupon discount then becomes an ordinary price component.
- **Order** can store a **frozen breakdown** of components per line instead of "total + discount percentage". This fits the existing rule of freezing the price at checkout.
- **Refund** can use the same breakdown to know how much net and how much VAT to return for a partial refund.

### The invoice example: what is possible

| Element | Possible? | Notes |
|---|---|---|
| Net, VAT, gross | **Yes** | decide whether catalog prices are net or gross, and where the VAT rate comes from (product, type) |
| Coupon discount as a component | **Yes** | requires a per-rule breakdown of the coupon result |
| Extra fees (packaging, small order) | **Yes** | ordinary components with conditions |
| Fees from other companies (payment provider, courier, platform subscription) | **Yes, as an internal cost** | not a line on the customer invoice. It is a company cost; it can be calculated but belongs in an internal report |
| Margin / markup | **Only after adding a purchase cost** | today the product knows only its selling price. Without a purchase cost there is nothing to compute a margin from |
| "What the company keeps" | **Partly** | computable as net revenue − known costs. This is not profit in the accounting sense (no fixed costs, income tax, etc.) |

### What not to do

- **Do not build a configurable formula engine in the admin panel** (calculators stored in the database, arbitrary dependencies between components). Component kinds defined in code with parameters in data are enough.
- **Do not recalculate pricing for placed orders.** The pricing result of a placed order is history, not something to reproduce.
- **Do not mix company costs with the customer invoice amount.** Customer invoice: net, VAT, discounts, customer fees. Partner costs: a separate internal view.
- **Do not treat the breakdown as a VAT invoice.** Legally compliant invoicing (numbering, corrections, buyer data, tax reporting) is a separate topic and does not follow from pricing alone.

---

## 5. Accounting: settlement ledger

### What to take (later)

| Building block | What it gives us |
|---|---|
| Account (receivables, revenue, VAT payable, partner liabilities) | a clear place for every amount from the price breakdown |
| A transaction of entries that must balance | no money gets "lost" between payment, VAT and revenue |
| A refund as a reversal entry | a refund does not erase history, it appends an opposite entry |
| A posting rule triggered by an event | e.g. a confirmed payment automatically creates a payment provider fee entry |
| Balance as of a date | settlement status with a partner at month end |

### When it makes sense

Only when **real settlements with partners** appear (e.g. payouts to sellers, settlement with a courier, periodic VAT summary). Without that it is an extra layer to maintain that nothing uses.

### What not to do

- **Do not build a full general ledger.** That is accounting software, not a shop.
- **Do not combine the ledger with price calculation.** The ledger records the pricing result; it does not compute it.

---

## 6. Other archetypes

| Archetype | Verdict | Why |
|---|---|---|
| Rules (AND / OR / NOT, result with a reason) | **Take the idea** | coupons already have rules; unify the result as "a list of findings with a severity" and reuse it in the catalog |
| Rules (scoring) | **Later** | customer loyalty levels computed from purchase history (section 11.3) |
| Quantity / units | **Optional** | useful when selling by weight or length. Today we sell pieces |
| Party (roles, relationships) | **Later** | company accounts with buyer and approver roles, B2B price lists (section 11.4) |
| Plan vs Execution | **Later** | monthly sales targets vs actual results, early alerts (section 11.5) |
| Inventory (units, batches) | **No** | we do not track serial numbers or expiry dates |
| Ordering | **No** | we already have a working flow for orders, payments and fulfilment |
| Graphs | **No** | apart from product relationships (section 3) the problem does not occur in a shop |

---

## 7. How to build products and prices

### 7.1 Today: a static product

```
Administrator ──enters──▶ Product
                           ├─ name, description
                           ├─ price (one number, overwritten)
                           ├─ one category
                           └─ tags, images

Storefront ──reads the price from the product──▶ displays it
Checkout   ──reads the price from the product──▶ freezes it in the reservation ──▶ order
Coupon     ──after the order is placed──▶ reduces the order total
```

Problems:

| Problem | Effect |
|---|---|
| The price is a product field and gets overwritten | no price history, no way to schedule a change or a time-limited promotion; "what did it cost a week ago" is known only from old orders |
| No product type | every product looks the same; no way to say shoes have a size and a laptop has a processor |
| The category does everything | one category is used both for navigation and as the only classification; adding new information means changing code and the database |
| The price has no structure | one amount, without net, VAT and components |
| Everything is entered manually, once | nothing happens on its own: a promotion requires a manual price change and a manual revert |

Good news: **the price enters the sales flow in one place** (the "get the product unit price" query used by the storefront and checkout). Only what stands behind this query has to change; cart, reservation, order and price-change detection keep working.

### 7.2 Target: a product built from blocks

```
┌────────────── CATALOG (what we sell) ────────────────┐   ┌──────── PRICE LIST (how much) ────────┐
│                                                      │   │                                       │
│  Product type (template)                             │   │  Price list entry for a product       │
│   └─ feature definitions: name, value type,          │   │   └─ price versions:                  │
│      constraint, required / optional                 │   │      amount, currency, valid from–to, │
│            │                                         │   │      who set it and when              │
│            ▼ follows the template                    │   │                                       │
│  Product                                             │   │  Pricing components:                  │
│   ├─ name, description, images                       │   │   base price ▶ promotion ▶ VAT        │
│   ├─ feature values (checked against the template)   │   │   ▶ result + breakdown                │
│   ├─ categories, tags (navigation only)              │   │                                       │
│   └─ sales conditions (channel, country, age, period)│   └───────────────────┬───────────────────┘
│                                                      │                       │
└──────────────────────────┬───────────────────────────┘                       │
                           │                                                   │
                           ▼                                                   ▼
           Storefront / cart / checkout:  "may it be sold?"  +  "how much does it cost now?"
                           │
                           ▼
           Order: frozen product description (with features) + frozen price breakdown
```

### 7.3 Building blocks one by one

**Product type (template)**
- The administrator creates types as data, not code: "Shoes", "Laptop", "Book".
- A type defines a list of features: `size` (list 36–47, required), `colour` (list, required), `material` (text, optional).
- A type is separate from a category. A category says **where** the product is visible in the shop. A type says **what** the product consists of.
- Changing a type (e.g. a new required feature) does not break existing products: they get a "complete this" warning, not a sales block.

**Product**
- Picks a type and fills in feature values. Each value is checked against the feature constraint.
- Publishing requires all required features. This fits the existing lifecycle: draft, published, discontinued.
- Sales conditions (channel, country, period) are product data and produce "allowed / not allowed / warning".

**Variant (only if needed)**
- If a feature changes stock or price (shoe size), it becomes a variant: a separate sellable unit with its own stock and price.
- If it does not (a cover colour without separate stock), it stays an ordinary feature.
- This is the biggest decision in this area because it affects inventory, cart and orders.

**Price list with versions**
- The price leaves the product and moves to the price list.
- A price change does not overwrite the previous one: it adds a new "valid from" version. The old one stays in history.
- A promotion is a version with a date range. When it ends, the base price applies again automatically; nobody has to remember to revert it.
- Selection rule: the version valid at a given moment applies; when versions overlap, the one that started most recently wins.
- A change can be scheduled in advance (new price from 1 January).
- Versions are never deleted or edited retroactively. A mistake is fixed with a new version.

**Pricing (components)**
- Answers "how much does this product cost now, in this context" and returns the amount together with a breakdown.
- To start: base price from the price list, promotion from the price list, VAT. Later: quantity tiers, extra fees, coupon discount as a component.
- The result goes to the storefront (display) and to checkout (freeze).

**Freezing in the order**
- The current rule stays: the price set at checkout does not change.
- We only extend what is frozen: besides name and image, also feature values and the price breakdown.
- Thanks to this **the product itself does not need versioning**. The history of "what exactly the customer bought" lives in the order.

### 7.4 What happens automatically that does not today

| Event | Today | After the change |
|---|---|---|
| Promotion ends | someone restores the price manually | the base price returns on its own |
| Scheduled price increase | someone enters it on the day | applies from the scheduled date |
| Missing required product information | the product can be published | publishing blocked with a reason |
| Seasonal product | manual publishing and unpublishing | available only within the sales period |
| Price changes between checkout start and payment | detected | detected the same way, no change in checkout |
| "What did it cost in March?" | only from old orders | read from the price list history |

### 7.5 Where to put the price list

| Option | What it means | Pros | Cons |
|---|---|---|---|
| A. In the catalog module | price list as a separate aggregate next to the product | least movement, one place for the administrator | the catalog grows; easy to glue price and product together again |
| B. A separate pricing module | the catalog says "what", pricing says "how much"; storefront and checkout ask pricing | clean boundary, natural home for VAT, promotions and later coupons | more work up front, one more integration |
| C. Price versions inside the product | a list of price versions within the product | fastest | does not solve components, VAT or promotions; sooner or later it has to be extracted anyway |

**Recommendation:** A to start, with the price list as a separate aggregate rather than a product field. Move to B when VAT, promotions and coupons as components arrive. Because the price enters the sales flow through a single query, moving from A to B does not require changes to the cart or orders.

### 7.6 Migrating existing data

- Every existing product gets a "General" type with no features. Nothing disappears and nothing stops selling.
- The current product price becomes the first price list version, valid from the product creation date.
- The product form stops editing the price; the price gets its own screen with history and scheduling.
- Coupons work unchanged until they are moved to a pricing component.

### 7.7 What not to do

- **Do not create free-form fields without constraints.** The value of this approach is the template and validation.
- **Do not version everything.** Versions are needed for prices. For the product description, freezing in the order is enough.
- **Do not build a price formula editor in the admin panel.** Component kinds live in code, the administrator sets their parameters.
- **Do not introduce variants "just in case".** Only when we sell something with separate stock per size or colour.
- **Do not change everything at once.** Each step in section 12 works on its own.

---

## 8. Product kinds and sales modes

### 8.1 The assortment the model must handle

| Kind | Example | Sales mode | What limits sales |
|---|---|---|---|
| Phone | smartphone 256 GB, black | pieces from stock | variant stock |
| Electronics, accessories | USB-C cable 2 m, case | pieces from stock | stock |
| Printed book | book with ISBN | pieces from stock | stock |
| E-book | the same book as a file | unlimited | nothing |
| Product-related service | extended warranty, screen protector fitting | unlimited | requires the product in the same order |
| In-person training | workshop on 15 March, 20 seats | limited seats | seat count and session date |
| Recorded online course | video course | unlimited | nothing |
| Live webinar | 100 seats | limited seats | seat count and date |
| Product bundle | phone + case + charger | follows its components | availability of each component |
| Training bundle | any 3 of 5 trainings | follows its components | free seats in the chosen trainings |

### 8.2 Should inventory care about online products?

**Agreed, with one refinement.** The criterion should not be "online or physical" but "what limits sales".

- Inventory counts pieces on a shelf: receipts, adjustments, reservations, dispatches, returns to stock. An e-book, a recorded course or a remote service has none of these. Inventory should not even know about them.
- **But "online" does not mean "unlimited".** A webinar for 100 people is online and has a limit. An in-person training is not goods and also has a limit.
- A seat limit **is not inventory**: seats do not return to a shelf, they expire after the session date, there are no deliveries or stocktaking, and they are counted per session, not per product. Forcing them into piece inventory would be the same mistake as forcing e-books there.

Therefore three sales modes, declared in the catalog (on the product type or the variant):

| Sales mode | Who guards availability | What happens in the cart and at checkout |
|---|---|---|
| **From stock** | inventory | as today: stock check, reservation, dispatch |
| **Unlimited** | nobody | no check, no reservation; only sales conditions (period, channel) |
| **Limited seats** | a separate, simple per-session seat mechanism | free seat check and a temporary seat hold |

A bundle has no stock of its own: it is available when its components are (unless it is physically pre-packed, in which case it is ordinary stock goods).

**Today:** digital product support is started, but in the wrong place and unfinished.

| Element | Today |
|---|---|
| "Digital product" flag | exists only in inventory |
| Catalog on publish | always sends "physical"; the product has no such information |
| Inventory for a digital product | skips the stock check but still creates a hold |
| Cart and checkout | require inventory stock; without it the product is "unavailable" |

Effect: today an e-book or a training cannot be sold without entering fake stock. A sales mode in the catalog removes this problem and takes knowledge out of inventory that does not belong there.

### 8.3 What the catalog will not describe well

| Case | Why it is not an ordinary catalog product | Where it fits |
|---|---|---|
| IMEI, serial number | concerns a specific piece, not the definition | inventory (dispatching a piece) and the order |
| Training participant data | entered at purchase, different for each person | order line (values entered by the customer) |
| Subscription (e.g. monthly course access) | requires recurring billing, renewals, cancellation | a separate recurring billing area, out of scope |
| Gift card | after the sale it acts as a means of payment, not goods | payments and the settlement ledger |
| Used goods with individual condition | each piece has a different description and price | a unit, not a product type |
| Calendar appointment (e.g. 1:1 consultation) | choosing a time slot is a booking, not a product feature | a separate booking mechanism, out of scope |

---

## 9. Gherkin: configuring products and sales

Assumptions for the scenarios: price list prices are gross in PLN; VAT rates are examples and are set in product or type data, not in code.

### 9.1 Product types and features

```gherkin
Feature: Product types and constrained features
  The administrator defines product templates as data.
  A product can be published only with a complete set of valid features.

  Background:
    Given a product type "Phone" with sales mode "from stock"
    And the type "Phone" has features:
      | feature     | value type | constraint                | required | variant |
      | storage     | list       | 128 GB, 256 GB, 512 GB    | yes      | yes     |
      | colour      | list       | black, white, blue        | yes      | yes     |
      | screen      | number     | 5.0 to 7.5 inches         | yes      | no      |
      | 5G notes    | text       | none                      | no       | no      |

  Scenario: Publishing a phone with all features
    Given the product "Phone X" of type "Phone" has a 6.1 inch screen
    And it has variants "128 GB / black" and "256 GB / white"
    When the administrator publishes the product
    Then the product is published
    And each variant has its own stock and its own price list entry

  Scenario: Publishing without a required feature is blocked
    Given the product "Phone X" of type "Phone" has no screen size
    When the administrator publishes the product
    Then publishing is rejected with the reason "missing required feature: screen"
    And the product remains a draft

  Scenario Outline: A feature value must satisfy its constraint
    When the administrator sets the feature "<feature>" to "<value>"
    Then the result is "<result>"

    Examples:
      | feature | value | result                                         |
      | screen  | 6.1   | saved                                          |
      | screen  | 9.0   | rejected: screen outside the range 5.0–7.5     |
      | storage | 1 TB  | rejected: allowed 128 GB, 256 GB, 512 GB       |

  Scenario: A new required feature on a type does not stop existing products from selling
    Given the product "Phone X" is published
    When the administrator adds a required feature "weight" to the type "Phone"
    Then "Phone X" is still on sale
    And "Phone X" has the warning "complete the feature: weight"

  Scenario: A new product without a newly required feature cannot be published
    Given the type "Phone" has a required feature "weight"
    And the product "Phone Y" of type "Phone" has no weight
    When the administrator publishes "Phone Y"
    Then publishing is rejected with the reason "missing required feature: weight"

  Scenario: The product type is independent of the category
    Given the product "Phone X" is of type "Phone"
    When the administrator moves it from the category "Smartphones" to "Outlet"
    Then the product keeps the same features and the same publishing rules
```

### 9.2 Books: one title, two sales modes

```gherkin
Feature: Printed book and e-book

  Background:
    Given a product type "Book" with features:
      | feature | value type | constraint         | required | variant |
      | ISBN    | text       | 13 digits          | yes      | no      |
      | author  | text       | none               | yes      | no      |
      | format  | list       | printed, e-book    | yes      | yes     |
    And the variant "printed" has sales mode "from stock"
    And the variant "e-book" has sales mode "unlimited"

  Scenario: An ISBN in the wrong format is rejected
    When the administrator sets the ISBN to "978-83-AB"
    Then saving is rejected with the reason "ISBN: 13 digits required"

  Scenario: The e-book does not go to inventory
    When the administrator publishes the book "Domain Modeling" in both formats
    Then inventory receives only the "printed" variant
    And the "e-book" variant is immediately available for sale

  Scenario: No printed copies does not block the e-book
    Given the stock of the "printed" variant is 0
    When the customer adds 1 "e-book" to the cart
    Then the line is added
    And no stock is checked or reserved

  Scenario: No printed copies blocks the printed version
    Given the stock of the "printed" variant is 0
    When the customer adds the "printed" variant to the cart
    Then the line is not added with the reason "out of stock"
```

### 9.3 Product-related services

```gherkin
Feature: Services sold together with a product

  Background:
    Given a service "Extended warranty 24 months" with sales mode "unlimited"
    And the warranty has a "requires" relationship with any product of type "Phone"
    And the service "Screen protector fitting" has a "requires" relationship with the product "Screen protector"

  Scenario: A service without the required product cannot pass checkout
    Given the cart contains only "Extended warranty 24 months"
    When the customer starts checkout
    Then checkout is blocked with the reason "the warranty requires a phone in the order"

  Scenario: A service with the required product
    Given the cart contains "Phone X 128 GB / black" and "Extended warranty 24 months"
    When the customer starts checkout
    Then checkout proceeds

  Scenario: The "requires" relationship points to a type, not a specific product
    When the administrator adds a new phone "Phone Y" of type "Phone"
    Then the warranty can be bought with "Phone Y" without changing the warranty configuration

  Scenario: The storefront suggests complementary services
    Given phones have a "complemented by" relationship with the warranty and the screen protector
    When the customer views "Phone X"
    Then the customer sees the suggestions "Extended warranty 24 months" and "Screen protector"
```

### 9.4 Trainings and seat limits

```gherkin
Feature: Trainings with and without seat limits

  Background:
    Given a product type "Training" with features:
      | feature  | value type | constraint            | required |
      | format   | list       | in-person, online     | yes      |
      | duration | number     | 1 to 40 hours         | yes      |
    And the training "DDD in practice" has a session on 2026-12-15 with 20 seats
    And sales for a session close 2 days before it starts

  Scenario: Buying a seat
    Given 19 seats are taken
    When the customer buys 1 seat
    Then the order is accepted
    And the session has 0 free seats
    And inventory is not involved

  Scenario: No free seats
    Given 20 seats are taken
    When the customer tries to add the training to the cart
    Then the line is not added with the reason "no free seats in the 2026-12-15 session"

  Scenario: A held seat is released after an unpaid order
    Given the customer holds a seat at checkout
    When the payment time runs out
    Then the seat is free again

  Scenario: Sales close before the session
    Given today is 2026-12-14
    When the customer views the training "DDD in practice"
    Then the 2026-12-15 session is unavailable with the reason "registration closed"

  Scenario: A recorded course has no limit
    Given the course "DDD – recording" has sales mode "unlimited"
    When 500 customers buy the course
    Then every order is accepted without an availability check

  Scenario: Participant data entered at purchase
    Given the type "Training" requires the participant's full name at purchase
    When the customer buys 2 seats and provides only one participant
    Then checkout is blocked with the reason "provide data for 2 participants"
```

### 9.5 Bundles

```gherkin
Feature: Product and training bundles

  Background:
    Given the bundle "Starter kit" consists of:
      | group       | items                                     | choice     |
      | phone       | any product of type "Phone"               | exactly 1  |
      | accessories | case, 65W charger, screen protector       | 0 to 2     |
    And the bundle has the rule "the case must match the phone model"

  Scenario: A phone bundle with accessories
    When the customer chooses "Phone X", the charger and a case for "Phone X"
    Then the bundle is valid

  Scenario: Too many accessories in the bundle
    When the customer chooses a phone and three accessories
    Then the bundle is rejected with the reason "accessories: choose 0 to 2"

  Scenario: A case that does not match the phone
    When the customer chooses "Phone X" and a case for "Phone Y"
    Then the bundle is rejected with the reason "the case must match the phone model"

  Scenario: Bundle availability follows its components
    Given the 65W charger has stock 0
    When the customer chooses the bundle with the 65W charger
    Then the bundle is not added with the reason "65W charger: out of stock"

  Scenario: The same bundle without the missing component can be added
    Given the 65W charger has stock 0
    When the customer chooses the bundle with a case instead of the charger
    Then the bundle is added

  Scenario: A training bundle with a choice
    Given the bundle "Architect path" requires exactly 3 different trainings out of 5
    When the customer chooses the same training twice and one other
    Then the bundle is rejected with the reason "trainings in the bundle must be different"

  Scenario: A training bundle holds seats in every chosen session
    Given the customer chose a bundle with three in-person trainings
    And every chosen session has free seats
    When the customer starts checkout
    Then one seat is held in each of the three sessions

  Scenario: A training bundle is rejected when any session is full
    Given the customer chose a bundle with three in-person trainings
    And one of the chosen sessions has no free seats
    When the customer starts checkout
    Then the whole bundle is rejected
    And no seat is held in any session

  Scenario: A mixed bundle combines different sales modes
    Given the bundle "Developer starter" contains a printed book and a recorded course
    When the customer buys the bundle
    Then inventory reserves only the book
    And the course is not checked for availability
```

### 9.6 Sales conditions and sales period

```gherkin
Feature: Product sales conditions
  A blocking condition prevents the purchase. A warning condition requires the customer's
  confirmation, and the confirmation is stored in the order line.

  Scenario: A seasonal product
    Given the product "Gadget advent calendar" is on sale from 2026-11-01 to 2026-12-24
    And today is 2026-12-27
    When the customer browses the storefront
    Then the product is not visible

  Scenario: Adding a product outside its sales period directly is rejected
    Given the product "Gadget advent calendar" is on sale from 2026-11-01 to 2026-12-24
    And today is 2026-12-27
    When the customer adds the product to the cart by its address
    Then the line is rejected with the reason "outside the sales period"

  Scenario: A product for a selected channel only
    Given the product "Web shop exclusive" has the channel condition "Web"
    When an order arrives through the API
    Then the line is rejected with the reason "product not available in this channel"

  Scenario: A warning condition requires confirmation
    Given the game "Cyber Shooter" has the PEGI 18 rating as a warning condition
    When the customer adds the game to the cart
    Then the customer sees the request "confirm that you are at least 18 years old"

  Scenario: A confirmed warning is stored in the order line
    Given the customer was asked to confirm the PEGI 18 rating for "Cyber Shooter"
    When the customer confirms
    Then the line is added
    And the confirmation with its date is stored in the order line

  Scenario: The result shows all reasons at once
    Given a product violates both the sales period and the channel condition
    When the customer tries to add it to the cart
    Then the customer sees both reasons, not only the first one
```

---

## 10. Gherkin: order pricing

Scope: price, coupons, VAT. Delivery and extra fees are only planned component kinds (an extension point), without logistics.

Order of pricing components:

```
price list price (version valid now)
  ▶ quantity price (tiers)
  ▶ bundle price (allocated to components)
  ▶ coupons (allocated to lines)
  ▶ [extension point] extra fees, delivery
  ▶ VAT derived from the gross amount of each line
  = total + breakdown
```

### 10.1 Base price, versions and freezing

```gherkin
Feature: Price list price with time versions

  Background:
    Given "Phone X 128 GB / black" has the price list versions:
      | gross price | valid from | valid to   | kind      |
      | 1230.00     | 2026-01-01 | open       | base      |
      | 1107.00     | 2026-11-27 | 2026-11-30 | promotion |
      | 1291.50     | 2027-01-01 | open       | base      |

  Scenario Outline: The version valid on a given day applies
    When the customer views the product on <day>
    Then the price is <price> PLN

    Examples:
      | day        | price   |
      | 2026-11-26 | 1230.00 |
      | 2026-11-28 | 1107.00 |
      | 2026-12-01 | 1230.00 |
      | 2027-01-02 | 1291.50 |

  Scenario: A price change does not remove history
    When the administrator adds a new price 1199.00 valid from 2026-12-10
    Then the previous versions remain in the price list history
    And no version can be deleted or changed retroactively

  Scenario: The price is frozen at checkout
    Given the customer started checkout at 2026-12-31 23:55 at the price 1230.00
    And the price 1291.50 applies from 2027-01-01 00:00
    When the customer pays at 2027-01-01 00:05, before the reservation expires
    Then the order has the price 1230.00

  Scenario: The cart always shows the current price
    Given the customer added the phone to the cart at the price 1230.00
    When the promotion 1107.00 starts before checkout
    Then the cart shows the price 1107.00
```

### 10.2 VAT

```gherkin
Feature: VAT derived from the gross price of each line

  Scenario: An order with different VAT rates
    Given the cart contains:
      | product                    | quantity | gross price | VAT rate |
      | Phone X 128 GB / black     | 1        | 1230.00     | 23%      |
      | Domain Modeling (printed)  | 1        | 52.50       | 5%       |
    When the customer starts checkout
    Then the pricing is:
      | line                       | net     | VAT    | gross   |
      | Phone X 128 GB / black     | 1000.00 | 230.00 | 1230.00 |
      | Domain Modeling (printed)  | 50.00   | 2.50   | 52.50   |
      | total                      | 1050.00 | 232.50 | 1282.50 |

  Scenario: The VAT rate comes from data, not code
    Given the type "Book" has a default rate of 5%
    And the product "Book-style planner" of type "Book" overrides the rate to 23%
    When the customer buys both products
    Then the book has 5% VAT and the planner has 23% VAT

  Scenario: Rounding per line
    Given a line price of 9.99 PLN gross at a 23% rate
    When pricing is calculated
    Then VAT is 1.87 and net is 8.12
    And net + VAT always equals the line gross amount exactly

  Scenario: A business customer sees net prices
    Given the customer provided a company tax ID
    When the customer views "Phone X 128 GB / black"
    Then the storefront shows 1000.00 PLN net as the main price
    And the gross amount to pay is the same as for a private customer
```

### 10.3 Quantity tiers and bundles

```gherkin
Feature: Quantity tiers and bundle prices

  Scenario Outline: The cable price depends on quantity
    Given "USB-C cable 2 m" costs 49.20 PLN per piece
    And from 5 pieces it costs 41.00 PLN per piece
    When the customer buys <quantity> pcs
    Then the line costs <total> PLN

    Examples:
      | quantity | total  |
      | 1        | 49.20  |
      | 4        | 196.80 |
      | 5        | 205.00 |
      | 10       | 410.00 |

  Scenario: A bundle has its own price and shows the saving
    Given "Phone X" costs 1230.00, the case 61.50 and the charger 123.00
    And the bundle "Starter kit" with these items costs 1353.00
    When the customer buys the bundle
    Then the customer pays 1353.00
    And sees a saving of 61.50 compared to buying separately

  Scenario: A bundle price with different VAT rates is allocated to components
    Given the bundle "Developer starter" contains:
      | component     | separate price | VAT rate |
      | printed book  | 105.00         | 5%       |
      | recorded course | 1230.00      | 23%      |
    And the bundle price is 1068.00
    When the customer buys the bundle
    Then the bundle price is allocated in proportion to the separate prices:
      | component       | share   | net    | VAT    |
      | printed book    | 84.00   | 80.00  | 4.00   |
      | recorded course | 984.00  | 800.00 | 184.00 |
```

### 10.4 Coupons

```gherkin
Feature: A coupon as a pricing component
  A coupon does not change a single order total. It creates a "discount" component
  on specific lines, and VAT is calculated from the amount after the discount.

  Background:
    Given the cart contains:
      | product                    | category    | gross price | VAT rate |
      | Phone X 128 GB / black     | Smartphones | 1230.00     | 23%      |
      | Domain Modeling (printed)  | Books       | 52.50       | 5%       |

  Scenario: A percentage coupon on the whole order
    When the customer applies the coupon "MINUS10" (10% off the order)
    Then the pricing is:
      | line                       | discount | gross   | net    | VAT    |
      | Phone X 128 GB / black     | -123.00  | 1107.00 | 900.00 | 207.00 |
      | Domain Modeling (printed)  | -5.25    | 47.25   | 45.00  | 2.25   |
      | total                      | -128.25  | 1154.25 | 945.00 | 209.25 |

  Scenario: A fixed-amount coupon is allocated proportionally to lines
    When the customer applies the coupon "MINUS100" (100.00 PLN off the order)
    Then the pricing is:
      | line                       | discount | gross   | net    | VAT    |
      | Phone X 128 GB / black     | -95.91   | 1134.09 | 922.02 | 212.07 |
      | Domain Modeling (printed)  | -4.09    | 48.41   | 46.10  | 2.31   |
      | total                      | -100.00  | 1182.50 | 968.12 | 214.38 |
    And the rounding remainder of the allocation goes to the last line

  Scenario: A category coupon applies only to matching lines
    When the customer applies the coupon "BOOKS20" (20% off the category "Books")
    Then the book costs 42.00 (net 40.00, VAT 2.00)
    And the phone still costs 1230.00

  Scenario: A coupon cannot reduce a line below zero
    When the customer applies a 60.00 PLN coupon on the category "Books"
    Then the discount on the book is 52.50
    And the remaining 7.50 PLN of the coupon is forfeited

  Scenario: A coupon with a free product
    When the customer applies the coupon "FREE-CASE"
    Then the order contains the line "Case" with the price 61.50 and the discount -61.50
    And the line has gross 0.00, net 0.00 and VAT 0.00

  Scenario: An unmet coupon condition gives a reason
    Given the coupon "MINUS10" requires an order of at least 1500.00 PLN
    When the customer applies the coupon to an order of 1282.50 PLN
    Then the coupon is rejected with the reason "minimum order value: 1500.00 PLN"
    And the pricing stays unchanged

  # Proposal: today an order holds one coupon.
  Scenario: Several coupons apply one after another
    When the customer applies the coupon "BOOKS20" and then "MINUS10"
    Then "MINUS10" is calculated from the prices after the "BOOKS20" discount
    And the breakdown shows a separate discount for each coupon

  Scenario: Removing a coupon restores the previous pricing
    Given the customer applied the coupon "MINUS10"
    When the customer removes the coupon before paying for the order
    Then the pricing returns to 1282.50 PLN
```

### 10.5 Services priced from another line

```gherkin
Feature: A service price calculated from a product price

  Scenario: The warranty costs a percentage of the phone price
    Given "Extended warranty 24 months" costs 10% of the price of the phone it is bought for
    When the customer buys "Phone X" for 1230.00 with the warranty
    Then the warranty costs 123.00

  Scenario: The warranty is calculated from the phone price before the coupon
    Given the customer applies a 10% coupon on the category "Smartphones"
    When the customer buys "Phone X" with the warranty
    Then the warranty costs 123.00
    And the coupon discount applies only to the phone
```

### 10.6 Order kinds

```gherkin
Feature: Pricing for different order kinds

  Scenario: An order with unlimited products only
    Given the cart contains an e-book and a recorded course
    When the customer places the order
    Then the pricing contains price, coupons and VAT
    And the order does not require an inventory reservation

  Scenario: A mixed order
    Given the cart contains a phone, an e-book and a training seat
    When the customer places the order
    Then pricing is calculated the same way for all lines
    And each line checks availability according to its own sales mode

  Scenario: An order in another currency
    Given the customer chose EUR as the currency
    When the customer places the order
    Then pricing is calculated in PLN and the EUR amount follows from the exchange rate frozen at checkout
```

### 10.7 Extension point: delivery and extra fees

```gherkin
Feature: New component kinds without changing existing ones
  Delivery and extra fees are component kinds that can be added later.
  Until then pricing simply does not contain them.

  Scenario: Without configured delivery, pricing does not show it
    Given no "delivery" component is configured
    When the customer places an order with a phone
    Then the breakdown does not contain a "delivery" line

  Scenario: A gift wrapping fee
    Given an extra fee "Gift wrapping" of 9.84 PLN at a 23% rate
    When the customer selects gift wrapping
    Then the breakdown contains the line "Gift wrapping" 9.84 (net 8.00, VAT 1.84)
    And the other pricing lines do not change

  Scenario: A small order fee applies below the threshold
    Given a fee "Small order" of 4.92 PLN for orders below 30.00 PLN
    When the customer buys only a USB-C adapter for 24.60 PLN
    Then the breakdown contains the fee "Small order" 4.92

  Scenario: No small order fee above the threshold
    Given a fee "Small order" of 4.92 PLN for orders below 30.00 PLN
    When the customer buys a USB-C cable 2 m for 49.20 PLN
    Then the breakdown does not contain the fee "Small order"

  Scenario: By default a coupon does not reduce extra fees
    Given the order contains a gift wrapping fee
    When the customer applies a 10% coupon on the order
    Then the discount applies only to products
    And the gift wrapping fee stays 9.84

  Scenario: A future free delivery coupon
    Given a "delivery" component is added in the future
    When the customer applies the coupon "FREE-DELIVERY"
    Then the coupon reduces only the "delivery" component
    And it requires no changes to the other components
```

---

## 11. Gherkin: ideas from other archetypes

Ideas that stay within e-commerce and do not require logistics. Each one is independent and optional.

### 11.1 Accounting: refunds and settlement

```gherkin
Feature: Refund as a reversal of the sale
  Requires the frozen price breakdown from section 10.

  Background:
    Given the order contains:
      | line                       | gross   | net     | VAT    |
      | Phone X 128 GB / black     | 1107.00 | 900.00  | 207.00 |
      | Domain Modeling (printed)  | 47.25   | 45.00   | 2.25   |
    And the order was paid

  Scenario: A partial refund returns the line amounts after discount
    When the refund for the book is approved
    Then the refund amount is 47.25
    And the refund breakdown is net 45.00 and VAT 2.25

  Scenario: A refund does not erase the sale
    When the refund for the book is approved
    Then the sale entries remain unchanged
    And an opposite entry for 47.25 is appended

  Scenario: A refund cannot exceed what was paid for the line
    Given the book was already refunded
    When a second refund for the book is requested
    Then the refund is rejected with the reason "line already fully refunded"

  Scenario: Balances always add up
    When the payment and the refund are recorded
    Then received payments − refunds = revenue + VAT payable
```

```gherkin
Feature: Payment provider fees as an internal cost

  Scenario: A confirmed payment records the provider fee
    Given the payment provider charges 1.5% of the payment amount
    When a payment of 1154.25 PLN is confirmed
    Then an internal cost "payment provider fee" of 17.31 PLN is recorded
    And the amount on the customer invoice does not change

  Scenario: A monthly summary of provider fees
    Given 120 payments were confirmed in November
    When the administrator opens the provider settlement for November
    Then the administrator sees the sum of fees and the list of payments it comes from
```

```gherkin
Feature: Gift card as a balance, not goods

  Scenario: Buying a gift card creates a balance
    When the customer buys a gift card for 200.00 PLN
    Then a gift card balance of 200.00 PLN is created
    And no stock is reserved

  Scenario: Paying partly with a gift card
    Given the gift card balance is 200.00 PLN
    When the customer pays for an order of 1154.25 PLN with the gift card and a card payment
    Then 200.00 PLN is charged from the gift card
    And 954.25 PLN is charged through the payment provider
    And the gift card balance is 0.00 PLN

  Scenario: A refund for an order paid with a gift card returns to the gift card
    Given an order paid fully with a gift card
    When the order is refunded
    Then the refund amount returns to the gift card balance
```

### 11.2 Margin from purchase cost

```gherkin
Feature: Margin from the purchase cost
  Requires a purchase cost for products. The margin is internal information only.

  Scenario: Margin on a sold line
    Given "Phone X 128 GB / black" has a purchase cost of 780.00 PLN net
    When the phone is sold for 900.00 PLN net after the coupon
    Then the line margin is 120.00 PLN (13.3% of the net selling price)

  Scenario: A coupon below the minimum margin needs approval
    Given the minimum margin for "Smartphones" is 5%
    When a coupon would reduce the phone margin to 2%
    Then the coupon is applied only after an administrator approves it

  Scenario: Margin report per category
    When the administrator opens the margin report for November
    Then the administrator sees revenue, purchase cost and margin per category
```

### 11.3 Rules (scoring): loyalty levels

```gherkin
Feature: Customer loyalty levels computed from purchase history

  Background:
    Given loyalty levels:
      | level  | condition                                            |
      | Silver | paid orders of at least 1000 PLN in the last 12 months |
      | Gold   | paid orders of at least 5000 PLN in the last 12 months |

  Scenario: A customer reaches a new level
    Given the customer paid for orders worth 4900 PLN in the last 12 months
    When an order of 200 PLN is paid
    Then the customer moves to the "Gold" level

  Scenario: Refunded orders do not count
    Given the customer paid for orders worth 5200 PLN in the last 12 months
    When an order of 400 PLN is refunded
    Then the customer drops to the "Silver" level

  Scenario: The level explains itself
    When the customer opens their account
    Then the customer sees the current level and how much is missing for the next one

  Scenario: A coupon available only from a given level
    Given the coupon "GOLD5" requires the "Gold" level
    When a "Silver" customer applies "GOLD5"
    Then the coupon is rejected with the reason "available from the Gold level"
```

### 11.4 Party: company accounts and roles

```gherkin
Feature: Company accounts with roles

  Background:
    Given the company "Acme Ltd" with the tax ID 1234567890
    And Anna has the role "buyer" in "Acme Ltd"
    And Piotr has the role "approver" in "Acme Ltd"

  Scenario: An order above the limit requires approval
    Given "Acme Ltd" requires approval for orders above 5000 PLN
    When Anna places an order of 6150 PLN for "Acme Ltd"
    Then the order waits for approval
    And Piotr gets an approval request

  Scenario: An approved order goes to payment
    Given Anna's order of 6150 PLN waits for approval
    When Piotr approves the order
    Then the order can be paid

  Scenario: A company price list
    Given "Acme Ltd" has a 5% discount price list for the category "Accessories"
    When Anna views a 61.50 PLN case
    Then Anna sees the price 58.43 PLN

  Scenario: Company data goes to the order
    When Anna places an order for "Acme Ltd"
    Then the order contains the company name and tax ID frozen at placement
```

### 11.5 Plan vs execution: sales targets

```gherkin
Feature: Sales targets compared with actual results

  Background:
    Given the sales target for "Smartphones" in December is 200000 PLN net

  Scenario: Progress against the target
    Given net sales of "Smartphones" from 1 to 15 December are 80000 PLN
    When the administrator opens the December targets
    Then the administrator sees 40% of the target achieved at 48% of the month elapsed
    And the category is marked "behind plan"

  Scenario: Refunds reduce the actual result
    Given net sales of "Smartphones" in December are 205000 PLN
    And refunds for "Smartphones" in December are 10000 PLN net
    When the administrator opens the December targets
    Then the actual result is 195000 PLN and the target is not met

  Scenario: Early alert on a large deviation
    Given the deviation alert threshold is 20%
    When, after half of the month, actual sales are more than 20% below the proportional plan
    Then the administrator gets an alert for "Smartphones"
```

### 11.6 Quantity: products sold by length or weight

```gherkin
Feature: Products sold in units other than pieces

  Scenario: A cable sold by the metre
    Given "Network cable Cat6" is sold by the metre for 4.92 PLN per metre
    And it can be bought in steps of 0.5 m, from 1 m to 100 m
    When the customer buys 12.5 m
    Then the line costs 61.50 PLN

  Scenario: A quantity outside the allowed step is rejected
    When the customer buys 12.3 m of "Network cable Cat6"
    Then the line is rejected with the reason "quantity must be a multiple of 0.5 m"

  Scenario: Stock is kept in the same unit
    Given the stock of "Network cable Cat6" is 300 m
    When an order for 12.5 m is placed
    Then 12.5 m is reserved and 287.5 m remains available
```

---

## 12. Suggested order

Each step delivers value on its own and does not require the next ones.

1. **A price list separated from the product, with time versions.** Foundation: price history, scheduled changes and time-limited promotions. The current price becomes the first version.
2. **A sales mode in the catalog** (from stock / unlimited). Unblocks selling e-books, courses and services without fake stock and removes this knowledge from inventory.
3. **Product types and constrained features**, with validation before publishing.
4. **Pricing with components** (base price, promotion, VAT) and a frozen price breakdown in the order.
5. **The coupon discount as a pricing component** allocated to lines instead of one order-level reduction.
6. **Sales conditions and sales period** with a "blocks / warns" result.
7. **Product relationships** (requires, complemented by) for product-related services.
8. **Seat limits** for trainings and webinars.
9. **Later, when needed:** variants, quantity tiers, bundles, extra fees, delivery, partner costs, purchase cost and margin, settlement ledger, gift cards, loyalty levels, company accounts, sales targets.

---

## 13. Decisions to make before starting

| # | Question | Affects |
|---|---|---|
| D1 | Are catalog prices net or gross? | pricing, storefront, breakdown |
| D2 | Where does the VAT rate come from: product, type, or both with an override? | catalog, pricing |
| D3 | How to store a zero or negative component (discount) when the shared money type allows only positive amounts? | shared types, pricing |
| D4 | Do variants (size, colour) have separate stock? | catalog, inventory, cart, order |
| D5 | Is the coupon discount calculated from net or from gross? | pricing, VAT |
| D6 | Which partner costs do we want to track (payment provider, courier, platform)? | internal pricing, ledger |
| D7 | Which currency are components stored in, given that the order and the payment have a currency? | pricing, exchange rates |
| D8 | Where to put the price list: in the catalog, in a separate module or in the product (section 7.5)? | module boundaries, integrations |
| D9 | Is the product type derived from the category or chosen independently? | product form, navigation |
| D10 | Who can change the price list and does a price change require approval? | permissions, admin panel |
| D11 | Is the sales mode declared on the product type or on the variant? (book: printed from stock, e-book unlimited) | catalog, inventory, checkout |
| D12 | How to allocate a bundle price and a fixed-amount coupon to lines: proportionally to prices (proposed in the scenarios) or differently? | VAT, partial refunds |
| D13 | Does a category coupon apply to bundle components, or is a bundle treated as a whole? | coupons, bundles |
| D14 | Can coupons reduce extra fees? (proposed in the scenarios: not by default) | coupons, extra fees |
| D15 | With several coupons: apply one after another from discounted prices (proposed) or each from the base price? | coupons |

# Copyright 2025 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""
Defines tools for brand search optimization agent.

This tool uses Google's public BigQuery dataset 'thelook_ecommerce' - no setup required!
Users can immediately test with brands like: Nike, Adidas, Levi's, Calvin Klein, etc.
"""

from google.cloud import bigquery

from ..shared_libraries import constants

# Initialize the BigQuery client outside the function
try:
    client = bigquery.Client(project=constants.PROJECT)
except Exception as e:
    print(f"Error initializing BigQuery client: {e}")
    client = None  # Set client to None if initialization fails


def get_categories_for_brand(brand: str):
    """
    Retrieves distinct product categories available for a given brand.
    This helps users select which category to analyze.
    
    Args:
        brand (str): The brand name (e.g., "Nike", "ASICS")
    
    Returns:
        str: A list of categories with product counts.
    """
    brand = brand.strip()
    
    if client is None:
        return "BigQuery client initialization failed. Please set GOOGLE_CLOUD_PROJECT in your .env file."
    
    query = """
        SELECT 
            category as Category,
            COUNT(*) as ProductCount
        FROM `bigquery-public-data.thelook_ecommerce.products`
        WHERE LOWER(brand) = LOWER(@brand)
        GROUP BY category
        ORDER BY ProductCount DESC
    """
    
    job_config = bigquery.QueryJobConfig(
        query_parameters=[
            bigquery.ScalarQueryParameter("brand", "STRING", brand),
        ]
    )
    
    try:
        query_job = client.query(query, job_config=job_config)
        results = query_job.result()
        
        categories = []
        for row in results:
            categories.append(f"- **{row.Category}** ({row.ProductCount} products)")
        
        if not categories:
            return f"No products found for brand '{brand}'. Try: Nike, Adidas, Levi's, Calvin Klein, Columbia, or Puma."
        
        return "\n".join(categories)
        
    except Exception as e:
        return f"Error querying categories: {str(e)}"


def get_product_details_for_brand(brand: str, category: str = None):
    """
    Retrieves real product data from Google's public 'thelook_ecommerce' dataset.
    Can optionally filter by category.
    
    Args:
        brand (str): The brand name to search for (e.g., "Nike", "ASICS")
        category (str, optional): The category to filter by (e.g., "Active", "Tops & Tees")
    
    Returns:
        str: A markdown table with product details filtered by category if specified.
    """
    brand = brand.strip()
    
    if client is None:
        return "BigQuery client initialization failed. Please set GOOGLE_CLOUD_PROJECT in your .env file."

    # Build query with optional category filter
    where_clause = f"WHERE LOWER(brand) = LOWER('{brand}')"
    if category:
        category = category.strip()
        where_clause += f" AND LOWER(category) = LOWER('{category}')"
    
    query = f"""
        SELECT 
            id,
            name as Title,
            category as Category,
            retail_price as Price,
            brand as Brand
        FROM `bigquery-public-data.thelook_ecommerce.products`
        {where_clause}
        LIMIT 10
    """
    
    try:
        query_job = client.query(query)
        results = query_job.result()
        
        # Build markdown table with public dataset schema
        markdown_table = "| ID | Title | Category | Price | Brand |\n"
        markdown_table += "|---|---|---|---|---|\n"
        
        product_count = 0
        for row in results:
            product_id = row.id
            title = row.Title
            cat = row.Category if row.Category else "N/A"
            price = f"${row.Price:.2f}" if row.Price else "N/A"
            brand_name = row.Brand
            
            markdown_table += f"| {product_id} | {title} | {cat} | {price} | {brand_name} |\n"
            product_count += 1
        
        if product_count == 0:
            category_note = f" in category '{category}'" if category else ""
            return f"No products found for brand '{brand}'{category_note}. Try popular brands like: Nike, Adidas, Levi's, Calvin Klein, Columbia, or Puma."
        
        return markdown_table
        
    except Exception as e:
        return f"Error querying public dataset: {str(e)}. Make sure GOOGLE_CLOUD_PROJECT is set in your .env file."

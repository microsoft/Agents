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
SerpAPI connector for reliable competitor data extraction.
Uses Google Shopping API to avoid bot detection and get structured data.
"""

import os
from typing import List, Dict, Union, Optional

def get_competitor_products(keyword: str, exclude_brand: Optional[str] = None) -> Union[List[Dict[str, str]], Dict[str, str]]:
    """
    Get competing products using SerpAPI Google Shopping.
    
    Args:
        keyword: Search term (e.g., "Nike Active")
        exclude_brand: Brand to filter out from results (e.g., "Nike")
    
    Returns:
        List of product dicts with 'title', 'price', 'source' keys, or error dict
    """
    api_key = os.getenv("SERPAPI_KEY")
    
    # Check if API key is configured
    if not api_key or api_key == "your_api_key_here_optional":
        return {
            "error": "SerpAPI key not configured",
            "message": "Set SERPAPI_KEY in .env for production-quality competitor data. Get free API key (100 searches/month) at https://serpapi.com/"
        }
    
    try:
        from serpapi import GoogleSearch
        
        print(f"📡 SerpAPI: Querying Google Shopping for '{keyword}'")
        
        params = {
            "engine": "google_shopping",
            "q": keyword,
            "api_key": api_key,
            "num": 30,  # Get more results to filter (need extra for 10 after brand filtering)
            "hl": "en",
            "gl": "us"
        }
        
        search = GoogleSearch(params)
        results = search.get_dict()
        
        shopping_results = results.get("shopping_results", [])
        
        if not shopping_results:
            return {
                "error": "No shopping results found",
                "message": f"SerpAPI returned no results for '{keyword}'"
            }
        
        competitors = []
        for item in shopping_results:
            title = item.get("title", "")
            
            # Skip if title is empty
            if not title or len(title) < 5:
                continue
            
            # Filter out the target brand if specified
            if exclude_brand and exclude_brand.lower() in title.lower():
                print(f"  ⊖ Filtered out: {title[:50]}...")
                continue
            
            price = item.get("price", item.get("extracted_price", "N/A"))
            source = item.get("source", "Unknown")
            
            # Extract keywords from title (split by common separators)
            keywords = []
            # Remove special characters and split
            cleaned_title = title.replace("-", " ").replace("|", " ").replace(",", " ")
            words = cleaned_title.split()
            # Get meaningful words (filter out very short ones)
            keywords = [w.strip() for w in words if len(w.strip()) > 2]
            
            competitors.append({
                "title": title,
                "price": str(price),
                "keywords": keywords,
                "source": source
            })
            
            print(f"  ✓ Found: {title[:60]}... - ${price}")
            
            if len(competitors) >= 10:
                break
        
        if not competitors:
            return {
                "error": "No competitor products found",
                "message": f"All results were from '{exclude_brand}' or filtered out"
            }
        
        print(f"✅ SerpAPI: Successfully extracted {len(competitors)} competitor products")
        return competitors
        
    except ImportError:
        return {
            "error": "SerpAPI library not installed",
            "message": "Run: pip install google-search-results"
        }
    except Exception as e:
        print(f"❌ SerpAPI error: {str(e)}")
        return {
            "error": "SerpAPI query failed",
            "message": str(e)
        }
